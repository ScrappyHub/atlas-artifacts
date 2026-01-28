using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Agent.IPC;

/// <summary>
/// Cross-platform IPC:
/// - Windows: Named Pipe (\\.\pipe\{name})
/// - macOS/Linux: Unix Domain Socket (file path)
///
/// Protocol: length-prefixed UTF-8 JSON frames.
/// </summary>
public sealed class IpcServer
{
    private readonly string _endpoint;
    private readonly CommandRouter _router;

    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public IpcServer(string endpoint, CommandRouter router)
    {
        _endpoint = endpoint;
        _router = router;
    }

    public Task StartAsync(CancellationToken externalCt)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _serverTask = RunWindowsPipeServerAsync(_endpoint, _cts.Token);
        else
            _serverTask = RunUnixSocketServerAsync(_endpoint, _cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_cts is null) return;

        try { _cts.Cancel(); } catch { /* best effort */ }

        if (_serverTask is not null)
        {
            try { await _serverTask.WaitAsync(ct); }
            catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Client helper (used by Program self-test).
    /// Connects to the endpoint, sends one request frame, reads one response frame.
    /// </summary>
    public async Task<IpcResponse> DispatchAsync(IpcRequest req, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await DispatchWindowsPipeAsync(_endpoint, req, ct);

        return await DispatchUnixSocketAsync(_endpoint, req, ct);
    }

    // -----------------------
    // Windows: Named Pipes
    // -----------------------
    private async Task RunWindowsPipeServerAsync(string pipeName, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
            );

            try
            {
                await pipe.WaitForConnectionAsync(ct);
            }
            catch
            {
                pipe.Dispose();
                break;
            }

            _ = Task.Run(async () =>
            {
                await using var p = pipe;
                try
                {
                    var reqJson = await ReadFrameAsync(p, ct);
                    var req = JsonSerializer.Deserialize<IpcRequest>(reqJson)
                              ?? throw new InvalidOperationException("invalid_request_json");

                    var resp = await _router.HandleAsync(req, ct);
                    var outJson = JsonSerializer.Serialize(resp);

                    await WriteFrameAsync(p, outJson, ct);
                }
                catch (Exception ex)
                {
                    var fallback = IpcResponse.Fail("unknown", $"internal_error: {ex.Message}");
                    try { await WriteFrameAsync(p, JsonSerializer.Serialize(fallback), ct); } catch { }
                }
            }, ct);
        }
    }

    private static async Task<IpcResponse> DispatchWindowsPipeAsync(string pipeName, IpcRequest req, CancellationToken ct)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(ct);

        var json = JsonSerializer.Serialize(req);
        await WriteFrameAsync(client, json, ct);

        var respJson = await ReadFrameAsync(client, ct);
        return JsonSerializer.Deserialize<IpcResponse>(respJson)
               ?? IpcResponse.Fail(req.RequestId, "invalid_response_json");
    }

    // -----------------------
    // Unix: Domain Sockets
    // -----------------------
    private async Task RunUnixSocketServerAsync(string socketPath, CancellationToken ct)
    {
        // Ensure directory exists
        var dir = Path.GetDirectoryName(socketPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        // Remove stale socket file
        try { if (File.Exists(socketPath)) File.Delete(socketPath); } catch { }

        var endPoint = new UnixDomainSocketEndPoint(socketPath);
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        listener.Bind(endPoint);
        listener.Listen(backlog: 64);

        while (!ct.IsCancellationRequested)
        {
            Socket? conn = null;
            try
            {
                conn = await listener.AcceptAsync(ct);
            }
            catch
            {
                conn?.Dispose();
                break;
            }

            _ = Task.Run(async () =>
            {
                await using var stream = new NetworkStream(conn, ownsSocket: true);
                try
                {
                    var reqJson = await ReadFrameAsync(stream, ct);
                    var req = JsonSerializer.Deserialize<IpcRequest>(reqJson)
                              ?? throw new InvalidOperationException("invalid_request_json");

                    var resp = await _router.HandleAsync(req, ct);
                    var outJson = JsonSerializer.Serialize(resp);

                    await WriteFrameAsync(stream, outJson, ct);
                }
                catch (Exception ex)
                {
                    var fallback = IpcResponse.Fail("unknown", $"internal_error: {ex.Message}");
                    try { await WriteFrameAsync(stream, JsonSerializer.Serialize(fallback), ct); } catch { }
                }
            }, ct);
        }

        // Best-effort cleanup
        try { if (File.Exists(socketPath)) File.Delete(socketPath); } catch { }
    }

    private static async Task<IpcResponse> DispatchUnixSocketAsync(string socketPath, IpcRequest req, CancellationToken ct)
    {
        var endPoint = new UnixDomainSocketEndPoint(socketPath);
        using var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await sock.ConnectAsync(endPoint, ct);

        await using var stream = new NetworkStream(sock, ownsSocket: false);

        var json = JsonSerializer.Serialize(req);
        await WriteFrameAsync(stream, json, ct);

        var respJson = await ReadFrameAsync(stream, ct);
        return JsonSerializer.Deserialize<IpcResponse>(respJson)
               ?? IpcResponse.Fail(req.RequestId, "invalid_response_json");
    }

    // -----------------------
    // Framing
    // -----------------------
    private static async Task<string> ReadFrameAsync(Stream s, CancellationToken ct)
    {
        var lenBytes = await ReadExactAsync(s, 4, ct);
        var len = BitConverter.ToInt32(lenBytes, 0);
        if (len <= 0 || len > 10_000_000)
            throw new InvalidOperationException($"invalid_frame_length: {len}");

        var payload = await ReadExactAsync(s, len, ct);
        return Encoding.UTF8.GetString(payload);
    }

    private static async Task WriteFrameAsync(Stream s, string text, CancellationToken ct)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var lenBytes = BitConverter.GetBytes(payload.Length);

        await s.WriteAsync(lenBytes, 0, lenBytes.Length, ct);
        await s.WriteAsync(payload, 0, payload.Length, ct);
        await s.FlushAsync(ct);
    }

    private static async Task<byte[]> ReadExactAsync(Stream s, int n, CancellationToken ct)
    {
        var buf = new byte[n];
        var off = 0;
        while (off < n)
        {
            var r = await s.ReadAsync(buf, off, n - off, ct);
            if (r <= 0) throw new EndOfStreamException("ipc_stream_closed");
            off += r;
        }
        return buf;
    }
}
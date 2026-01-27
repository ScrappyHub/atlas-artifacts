using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Atlas.Agent.IPC;

public sealed class IpcServer
{
    private readonly string _pipeName;
    private readonly CommandRouter _router;

    public IpcServer(string pipeName, CommandRouter router)
    {
        _pipeName = pipeName;
        _router = router;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
            );

            await pipe.WaitForConnectionAsync(ct);

            _ = Task.Run(async () =>
            {
                await using var p = pipe;

                try
                {
                    var reqJson = await ReadFrameAsync(p, ct);
                    var req = JsonSerializer.Deserialize<IpcRequest>(reqJson, JsonOpts.Serializer)
                              ?? throw new InvalidOperationException("Invalid request JSON");

                    var resp = await _router.HandleAsync(req, ct);
                    var outJson = JsonSerializer.Serialize(resp, JsonOpts.Serializer);

                    await WriteFrameAsync(p, outJson, ct);
                }
                catch (OperationCanceledException)
                {
                    // best effort: allow cancellation to exit cleanly
                }
                catch (Exception ex)
                {
                    var fallback = new IpcResponse(
                        request_id: "unknown",
                        ok: false,
                        error: new IpcError("internal_error", ex.Message),
                        data: null
                    );

                    var outJson = JsonSerializer.Serialize(fallback, JsonOpts.Serializer);
                    try { await WriteFrameAsync(p, outJson, ct); } catch { /* best effort */ }
                }
            }, ct);
        }
    }

    private static async Task<string> ReadFrameAsync(Stream s, CancellationToken ct)
    {
        var lenBytes = await ReadExactAsync(s, 4, ct);
        var len = BitConverter.ToInt32(lenBytes, 0);
        if (len <= 0 || len > 10_000_000)
            throw new InvalidOperationException($"Invalid frame length: {len}");

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
            if (r <= 0) throw new EndOfStreamException("Pipe closed while reading frame");
            off += r;
        }
        return buf;
    }
}

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

    public async Task RunAsync()
    {
        while (true)
        {
            var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
            );

            await pipe.WaitForConnectionAsync();

            _ = Task.Run(async () =>
            {
                await using var p = pipe;
                try
                {
                    var reqJson = await ReadAllAsync(p);
                    var req = JsonSerializer.Deserialize<IpcRequest>(reqJson, JsonOpts.Serializer)
                              ?? throw new InvalidOperationException("Invalid request JSON");

                    var resp = await _router.HandleAsync(req);
                    var outJson = JsonSerializer.Serialize(resp, JsonOpts.Serializer);
                    await WriteAllAsync(p, outJson);
                }
                catch (Exception ex)
                {
                    // Best-effort error response
                    var fallback = new IpcResponse(
                        request_id: "unknown",
                        ok: false,
                        error: new IpcError("internal_error", ex.Message),
                        data: null
                    );

                    var outJson = JsonSerializer.Serialize(fallback, JsonOpts.Serializer);
                    await WriteAllAsync(p, outJson);
                }
            });
        }
    }

    private static async Task<string> ReadAllAsync(Stream s)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await s.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, read);
            if (!s.CanRead) break;
            // Named pipe read may end when client closes. Client should close after write.
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task WriteAllAsync(Stream s, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await s.WriteAsync(bytes, 0, bytes.Length);
        await s.FlushAsync();
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Agent.Engines.Winget;

namespace Atlas.Agent.IPC;

public sealed class CommandRouter
{
    private readonly WingetScan _winget;

    public CommandRouter(WingetScan winget)
    {
        _winget = winget;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest req, CancellationToken ct)
    {
        req = req with { RequestId = req.RequestId ?? Guid.NewGuid().ToString("n") };

        return req.Command switch
        {
            IpcCommand.Ping => new IpcResponse(true, "pong", null, req.RequestId, DateTimeOffset.UtcNow),

            IpcCommand.ScanWinget => new IpcResponse(
                true,
                "ok",
                await _winget.ScanAsync(ct),
                req.RequestId,
                DateTimeOffset.UtcNow),

            _ => new IpcResponse(false, $"Unknown command: {req.Command}", null, req.RequestId, DateTimeOffset.UtcNow)
        };
    }
}
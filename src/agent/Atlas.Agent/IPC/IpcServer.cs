using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Agent.IPC;

public sealed class IpcServer
{
    private readonly CommandRouter _router;

    public IpcServer(CommandRouter router) => _router = router;

    public Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine("IPC server stub started (no listener).");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        Console.WriteLine("IPC server stub stopped.");
        return Task.CompletedTask;
    }

    public Task<IpcResponse> DispatchAsync(IpcRequest req, CancellationToken ct)
        => _router.HandleAsync(req, ct);
}
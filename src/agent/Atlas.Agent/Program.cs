using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Agent.Engines.Winget;
using Atlas.Agent.IPC;
using Atlas.Agent.Licensing;
using Atlas.Agent.Logging;
using Atlas.Agent.Storage;

namespace Atlas.Agent;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Canonical paths (cross-platform)
        var paths = AppPaths.Resolve();
        AppPaths.EnsureAll(paths);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Licensing (local, offline). Keep your existing implementation.
        var license = new LicenseService();
        license.Load();

        // Storage/artifacts
        var runStore = new RunStore(paths.DbPath);
        var artifacts = new ArtifactWriter(paths.RunsRoot);

        // Engine (Windows-only execution; still safe to construct everywhere)
        var winget = new WingetScan(new WingetRunner());

        // IPC
        var router = new CommandRouter(winget);
        var ipc = new IpcServer(router);

        await ipc.StartAsync(cts.Token);

        // Quick self-test: ping + scan
        var ping = await ipc.DispatchAsync(new IpcRequest(IpcCommand.Ping), cts.Token);
        Console.WriteLine($"PING: ok={ping.Ok} msg={ping.Message}");

        var scan = await ipc.DispatchAsync(new IpcRequest(IpcCommand.ScanWinget), cts.Token);
        var runId = Guid.NewGuid().ToString("n");
        runStore.Insert(runId, "winget_scan", scan);
        artifacts.WriteJson(runId, "winget_scan", scan);

        Console.WriteLine("Atlas.Agent baseline finished.");
        await ipc.StopAsync(cts.Token);

        return 0;
    }
}

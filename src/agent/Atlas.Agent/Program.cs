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
        AppPaths.EnsureAll();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var license = new LicenseService();
        license.Load();

        var runStore = new RunStore(AppPaths.DbPath);
        var artifacts = new ArtifactWriter(AppPaths.RunsRoot);

        var winget = new WingetScan(new WingetRunner());
        var router = new CommandRouter(winget);
        var ipc = new IpcServer(router);

        await ipc.StartAsync(cts.Token);

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
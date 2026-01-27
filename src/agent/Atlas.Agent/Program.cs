using System;
using System.Threading.Tasks;
using Atlas.Agent.Engines.Winget;
using Atlas.Agent.IPC;
using Atlas.Agent.Licensing;
using Atlas.Agent.Security;
using Atlas.Agent.Storage;

namespace Atlas.Agent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Atlas Update Agent starting...");

        var paths = AppPaths.Resolve();
        paths.EnsureAll();

        // DB bootstrap (local-only, no network)
        await SqliteBootstrap.EnsureDbAsync(paths.DbPath);

        // Device identity (stable + deterministic)
        var deviceId = DeviceIdentity.GetOrCreateDeviceId(paths.DeviceSaltPath);
        Console.WriteLine($"device_id = {(deviceId.Length >= 12 ? deviceId[..12] + "…" : deviceId)}");

        // Licensing (offline verify + tier caps)
        var licenseService = new LicenseService(paths);

        // Engines (wire what exists now)
        var winget = new WingetScan(new WingetRunner());

        // Router is the enforcement boundary
        var router = new CommandRouter(paths, deviceId, licenseService, winget);

        // IPC server
        var server = new IpcServer(pipeName: "atlas-update", router);

        Console.WriteLine(@"IPC listening on named pipe: \\.\pipe\atlas-update");
        await server.RunAsync();
    }
}

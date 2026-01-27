using System;
using System.IO;
using System.Threading.Tasks;
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

        // Canonical paths + directories
        var paths = AppPaths.Resolve();
        Directory.CreateDirectory(paths.DataDir);
        Directory.CreateDirectory(paths.ArtifactsDir);
        Directory.CreateDirectory(paths.LogsDir);

        // DB bootstrap (local, offline)
        await SqliteBootstrap.EnsureDbAsync(paths.DbPath);

        // Stable device identity (offline)
        var deviceId = DeviceIdentity.GetOrCreateDeviceId(paths.DeviceSaltPath);
        Console.WriteLine($"device_id = {deviceId[..12]}…");

        // Licensing (local + offline)
        // Phase 1: token/license status model; later upgraded to signed license payload + device binding.
        var licenseService = new LicenseService(paths);
        var licenseStatus = await licenseService.GetLicenseStatusAsync(deviceId);
        Console.WriteLine($"license = {licenseStatus.Status} tier={licenseStatus.Tier} features={licenseStatus.Features}");

        // IPC
        var router = new CommandRouter(paths, deviceId, licenseService);
        var server = new IpcServer(pipeName: "atlas-update", router);

        Console.WriteLine(@"IPC listening on named pipe: \\.\pipe\atlas-update");
        await server.RunAsync();
    }
}

<<<<<<< HEAD
using Atlas.Agent.IPC;
using Atlas.Agent.Storage;
using Atlas.Agent.Licensing;
using Atlas.Agent.Security;

namespace Atlas.Agent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Atlas Update Agent starting...");

        var paths = AppPaths.Resolve();
        Directory.CreateDirectory(paths.DataDir);
        Directory.CreateDirectory(paths.ArtifactsDir);

        // DB bootstrap
        await SqliteBootstrap.EnsureDbAsync(paths.DbPath);

        // Device identity
        var deviceId = DeviceIdentity.GetOrCreateDeviceId(paths.DeviceSaltPath);
        Console.WriteLine($"device_id = {deviceId[..12]}…");

        // Licensing (Phase 1 local token file)
        var licenseService = new LicenseService(paths);
        var licenseStatus = await licenseService.GetLicenseStatusAsync(deviceId);

        // Start IPC server
        var router = new CommandRouter(paths, deviceId, licenseService);
        var server = new IpcServer(pipeName: "atlas-update", router);

        Console.WriteLine("IPC listening on named pipe: \\\\.\\pipe\\atlas-update");
        await server.RunAsync();
    }
}
=======
using Atlas.Agent.IPC;
using Atlas.Agent.Storage;
using Atlas.Agent.Licensing;
using Atlas.Agent.Security;

namespace Atlas.Agent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine(""Atlas Update Agent starting..."");

        var paths = AppPaths.Resolve();
        Directory.CreateDirectory(paths.DataDir);
        Directory.CreateDirectory(paths.ArtifactsDir);

        await SqliteBootstrap.EnsureDbAsync(paths.DbPath);

        var deviceId = DeviceIdentity.GetOrCreateDeviceId(paths.DeviceSaltPath);
        Console.WriteLine($""device_id = {deviceId[..12]}…"");

        var licenseService = new LicenseService(paths);
        _ = await licenseService.GetLicenseStatusAsync(deviceId);

        var router = new CommandRouter(paths, deviceId, licenseService);
        var server = new IpcServer(pipeName: ""atlas-update"", router);

        Console.WriteLine(""IPC listening on named pipe: \\\\.\\pipe\\atlas-update"");
        await server.RunAsync();
    }
}
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

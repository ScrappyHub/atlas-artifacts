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

        // Canonical disk layout (ProgramData-based)
        var paths = AppPaths.Resolve();
        Directory.CreateDirectory(paths.DataDir);
        Directory.CreateDirectory(paths.ArtifactsDir);
        Directory.CreateDirectory(paths.LicensesDir);

        // DB bootstrap (local, offline)
        await SqliteBootstrap.EnsureDbAsync(paths.DbPath);

        // Device identity (local, deterministic)
        var deviceId = DeviceIdentity.GetOrCreateDeviceId(paths.DeviceSaltPath);
        Console.WriteLine($"device_id = {deviceId[..12]}…");

        // Licensing (local file + signature verification; offline enforcement)
        var licenseService = new LicenseService(paths);

        // Router is the enforcement boundary.
        var router = new CommandRouter(paths, deviceId, licenseService);

        // IPC named pipe server
        var server = new IpcServer(pipeName: "atlas-update", router);

        Console.WriteLine(@"IPC listening on named pipe: \\.\pipe\atlas-update");

        // Long-running server loop (Ctrl+C handled by host/svc wrapper in production)
        await server.RunAsync();
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Atlas.Agent;

/// <summary>
/// Canonical disk layout resolver.
/// Must be deterministic, offline-friendly, and cross-platform.
/// </summary>
public static class AppPaths
{
    public sealed record PathSet(
        string DataRoot,
        string LicensesRoot,
        string LicenseJsonPath,
        string LicenseSigPath,
        string RunsRoot,
        string LogsRoot,
        string DbPath,
        string DeviceSaltPath
    );

    /// <summary>
    /// Canonical resolver:
    /// - Windows:   C:\ProgramData\Atlas\Agent
    /// - macOS:     /Library/Application Support/Atlas/Agent
    /// - Linux:     /var/lib/atlas/agent
    ///
    /// If the process cannot write to the canonical system path, we fall back to a user path:
    /// - macOS:     ~/Library/Application Support/Atlas/Agent
    /// - Linux:     ~/.local/share/atlas/agent
    /// - Windows:   %LOCALAPPDATA%\Atlas\Agent
    /// </summary>
    public static PathSet Resolve()
    {
        var systemRoot = GetSystemDataRoot();
        var root = CanWriteTo(systemRoot) ? systemRoot : GetUserDataRoot();

        var licenses = Path.Combine(root, "licenses");
        var runs = Path.Combine(root, "runs");
        var logs = Path.Combine(root, "logs");

        var licenseJson = Path.Combine(licenses, "atlas.license.json");
        var licenseSig = Path.Combine(licenses, "atlas.license.sig");

        var db = Path.Combine(root, "atlas.db");
        var deviceSalt = Path.Combine(root, "device.salt");

        return new PathSet(
            DataRoot: root,
            LicensesRoot: licenses,
            LicenseJsonPath: licenseJson,
            LicenseSigPath: licenseSig,
            RunsRoot: runs,
            LogsRoot: logs,
            DbPath: db,
            DeviceSaltPath: deviceSalt
        );
    }

    public static void EnsureAll(PathSet p)
    {
        Directory.CreateDirectory(p.DataRoot);
        Directory.CreateDirectory(p.LicensesRoot);
        Directory.CreateDirectory(p.RunsRoot);
        Directory.CreateDirectory(p.LogsRoot);
    }

    private static string GetSystemDataRoot()
    {
        // Windows canonical: C:\ProgramData\Atlas\Agent
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "Atlas", "Agent");
        }

        // macOS canonical: /Library/Application Support/Atlas/Agent
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(Path.DirectorySeparatorChar.ToString(), "Library", "Application Support", "Atlas", "Agent");
        }

        // Linux canonical: /var/lib/atlas/agent
        // (lowercase is conventional on Linux)
        return Path.Combine(Path.DirectorySeparatorChar.ToString(), "var", "lib", "atlas", "agent");
    }

    private static string GetUserDataRoot()
    {
        // Windows fallback: %LOCALAPPDATA%\Atlas\Agent
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "Atlas", "Agent");
        }

        // macOS fallback: ~/Library/Application Support/Atlas/Agent
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Atlas", "Agent");
        }

        // Linux fallback: ~/.local/share/atlas/agent
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, "atlas", "agent");

        var homeLinux = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeLinux, ".local", "share", "atlas", "agent");
    }

    private static bool CanWriteTo(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);

            var probe = Path.Combine(dir, ".write_probe");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);

            return true;
        }
        catch
        {
            return false;
        }
    }
}

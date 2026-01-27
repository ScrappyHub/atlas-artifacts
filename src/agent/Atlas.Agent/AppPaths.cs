using System;
using System.IO;

namespace Atlas.Agent;

public sealed record AppPaths(
    string RootDir,
    string DataDir,
    string LicensesDir,
    string RunsDir,
    string LogsDir,
    string DbPath,
    string DeviceSaltPath,
    string LicenseJsonPath,
    string LicenseSigPath
)
{
    public static AppPaths Resolve()
    {
        // Canonical base: C:\ProgramData\Atlas\Agent
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var root = Path.Combine(programData, "Atlas", "Agent");

        var dataDir = root; // keep "Agent" root as the data dir
        var licensesDir = Path.Combine(root, "licenses");
        var runsDir = Path.Combine(root, "runs");
        var logsDir = Path.Combine(root, "logs");

        var dbPath = Path.Combine(root, "atlas.db");
        var deviceSaltPath = Path.Combine(root, "device.salt");

        var licenseJson = Path.Combine(licensesDir, "atlas.license.json");
        var licenseSig = Path.Combine(licensesDir, "atlas.license.sig");

        return new AppPaths(
            RootDir: root,
            DataDir: dataDir,
            LicensesDir: licensesDir,
            RunsDir: runsDir,
            LogsDir: logsDir,
            DbPath: dbPath,
            DeviceSaltPath: deviceSaltPath,
            LicenseJsonPath: licenseJson,
            LicenseSigPath: licenseSig
        );
    }

    public void EnsureAll()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(LicensesDir);
        Directory.CreateDirectory(RunsDir);
        Directory.CreateDirectory(LogsDir);
    }
}

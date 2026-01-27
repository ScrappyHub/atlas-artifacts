using System;
using System.IO;

namespace Atlas.Agent;

public static class AppPaths
{
    public static string CompanyRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Atlas");

    public static string AgentRoot    => Path.Combine(CompanyRoot, "Agent");
    public static string RunsRoot     => Path.Combine(AgentRoot, "runs");
    public static string DbPath       => Path.Combine(AgentRoot, "atlas.db");
    public static string LogsRoot     => Path.Combine(AgentRoot, "logs");

    // Canonical licensing paths
public static string LicensesRoot => Path.Combine(AgentRoot, "licenses");
public static string LicenseJsonPath => Path.Combine(LicensesRoot, "atlas.license.json");
public static string LicenseSigPath  => Path.Combine(LicensesRoot, "atlas.license.sig");

    public static void EnsureAll()
    {
        Directory.CreateDirectory(CompanyRoot);
        Directory.CreateDirectory(AgentRoot);
        Directory.CreateDirectory(RunsRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(LicensesRoot);
    }
}


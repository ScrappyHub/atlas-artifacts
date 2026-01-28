using System;
using System.IO;
using Microsoft.Data.Sqlite;

static class Program
{
    static int Main(string[] args)
    {
        try
        {
            var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
            var dbPath = GetDbPath();

            if (!File.Exists(dbPath))
            {
                Console.Error.WriteLine($"ERR: DB not found: {dbPath}");
                return 2;
            }

            switch (cmd)
            {
                case "drop-activations":
                    DropActivations(dbPath);
                    Console.WriteLine("OK: dropped license_activations");
                    return 0;

                case "show-activations":
                    ShowActivations(dbPath);
                    return 0;

                case "help":
                default:
                    Console.WriteLine("Atlas.DbTool");
                    Console.WriteLine("  drop-activations   - DROP TABLE IF EXISTS license_activations");
                    Console.WriteLine("  show-activations   - dump counts + rows (prints empty if table missing)");
                    Console.WriteLine();
                    Console.WriteLine($"DB: {dbPath}");
                    return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERR: " + ex);
            return 1;
        }
    }

    static string GetDbPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "Atlas", "Agent", "atlas.db");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "atlas", "agent", "atlas.db");
    }

    static void DropActivations(string dbPath)
    {
        using var cn = new SqliteConnection($"Data Source={dbPath}");
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS license_activations;";
        cmd.ExecuteNonQuery();
    }

    static void ShowActivations(string dbPath)
    {
        using var cn = new SqliteConnection($"Data Source={dbPath}");
        cn.Open();

        // If table doesn't exist, print empty and return (so drops are clean)
        try
        {
            using var probe = cn.CreateCommand();
            probe.CommandText = "SELECT 1 FROM license_activations LIMIT 1;";
            probe.ExecuteScalar();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("COUNTS:");
            Console.WriteLine();
            Console.WriteLine("ROWS:");
            return;
        }

        Console.WriteLine("COUNTS:");
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT license_id, COUNT(*) AS devices
FROM license_activations
GROUP BY license_id
ORDER BY license_id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                Console.WriteLine($"  {r.GetString(0)} devices={r.GetInt64(1)}");
        }

        Console.WriteLine();
        Console.WriteLine("ROWS:");
        using (var cmd2 = cn.CreateCommand())
        {
            cmd2.CommandText = @"
SELECT license_id, device_id, first_seen_utc, last_seen_utc
FROM license_activations
ORDER BY license_id, device_id;";
            using var r2 = cmd2.ExecuteReader();
            while (r2.Read())
                Console.WriteLine($"  {r2.GetString(0)}  {r2.GetString(1)}  first={r2.GetString(2)}  last={r2.GetString(3)}");
        }
    }
}
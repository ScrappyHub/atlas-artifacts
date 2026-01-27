using System.IO;
using Microsoft.Data.Sqlite;

namespace Atlas.Agent.Storage;

public static class SqliteBootstrap
{
    public static void EnsureCreated(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS runs(
  run_id TEXT PRIMARY KEY,
  created_utc TEXT NOT NULL,
  kind TEXT NOT NULL,
  payload_json TEXT NOT NULL
);";
        cmd.ExecuteNonQuery();
    }
}
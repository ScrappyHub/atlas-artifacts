using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Atlas.Agent.Storage;

public sealed class RunStore
{
    private readonly string _dbPath;

    public RunStore(string dbPath)
    {
        _dbPath = dbPath;
        SqliteBootstrap.EnsureCreated(_dbPath);
    }

    public void Insert(string runId, string kind, object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT OR REPLACE INTO runs(run_id, created_utc, kind, payload_json)
VALUES ($id, $utc, $kind, $json);";
        cmd.Parameters.AddWithValue("$id", runId);
        cmd.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.ExecuteNonQuery();
    }
}
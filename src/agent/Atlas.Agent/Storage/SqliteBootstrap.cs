<<<<<<< HEAD
using Microsoft.Data.Sqlite;

namespace Atlas.Agent.Storage;

public static class SqliteBootstrap
{
    public static async Task EnsureDbAsync(string dbPath)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        var sql = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS runs (
  run_id TEXT PRIMARY KEY,
  run_type TEXT NOT NULL CHECK(run_type IN ('scan_run','apply_run')),
  created_at TEXT NOT NULL,
  outcome TEXT NOT NULL CHECK(outcome IN ('success','partial','failed','deferred')),
  summary_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS run_artifacts (
  run_id TEXT NOT NULL,
  artifact_key TEXT NOT NULL,
  path TEXT NOT NULL,
  sha256 TEXT NOT NULL,
  created_at TEXT NOT NULL,
  PRIMARY KEY (run_id, artifact_key),
  FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
);";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
=======
using Microsoft.Data.Sqlite;

namespace Atlas.Agent.Storage;

public static class SqliteBootstrap
{
    public static async Task EnsureDbAsync(string dbPath)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, ""..\\..\\..\\..\\..\\schemas\\atlas_sqlite_schema.sql""));
        // If path resolution fails in your environment, we can replace with embedded SQL.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

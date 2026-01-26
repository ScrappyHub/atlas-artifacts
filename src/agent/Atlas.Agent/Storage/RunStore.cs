using System.Text.Json;
using Microsoft.Data.Sqlite;
using Atlas.Agent.IPC;

namespace Atlas.Agent.Storage;

public sealed class RunStore
{
    private readonly string _dbPath;

    public RunStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    private SqliteConnection Open()
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
        var c = new SqliteConnection(cs);
        c.Open();
        return c;
    }

    public void InsertRun(string runId, string runType, string createdAt, string outcome, object summary)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO runs(run_id, run_type, created_at, outcome, summary_json)
VALUES ($id, $type, $at, $outcome, $summary);";
        cmd.Parameters.AddWithValue("$id", runId);
        cmd.Parameters.AddWithValue("$type", runType);
        cmd.Parameters.AddWithValue("$at", createdAt);
        cmd.Parameters.AddWithValue("$outcome", outcome);
        cmd.Parameters.AddWithValue("$summary", JsonSerializer.Serialize(summary, JsonOpts.Serializer));
        cmd.ExecuteNonQuery();
    }

    public void InsertArtifact(string runId, string artifactKey, string path, string sha256, string createdAt)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO run_artifacts(run_id, artifact_key, path, sha256, created_at)
VALUES ($run, $key, $path, $sha, $at);";
        cmd.Parameters.AddWithValue("$run", runId);
        cmd.Parameters.AddWithValue("$key", artifactKey);
        cmd.Parameters.AddWithValue("$path", path);
        cmd.Parameters.AddWithValue("$sha", sha256);
        cmd.Parameters.AddWithValue("$at", createdAt);
        cmd.ExecuteNonQuery();
    }

    public List<object> GetRunHistory(int limit = 50)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT run_id, run_type, created_at, outcome
FROM runs
ORDER BY created_at DESC
LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);

        var outList = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            outList.Add(new
            {
                run_id = r.GetString(0),
                run_type = r.GetString(1),
                created_at = r.GetString(2),
                outcome = r.GetString(3)
            });
        }
        return outList;
    }

    public object? GetRunDetails(string runId)
    {
        using var conn = Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT run_id, run_type, created_at, outcome, summary_json FROM runs WHERE run_id=$id;";
        cmd.Parameters.AddWithValue("$id", runId);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var summaryJson = r.GetString(4);

        // Artifacts
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = @"SELECT artifact_key, path, sha256, created_at FROM run_artifacts WHERE run_id=$id ORDER BY artifact_key;";
        cmd2.Parameters.AddWithValue("$id", runId);

        var artifacts = new List<object>();
        using var r2 = cmd2.ExecuteReader();
        while (r2.Read())
        {
            artifacts.Add(new
            {
                artifact_key = r2.GetString(0),
                path = r2.GetString(1),
                sha256 = r2.GetString(2),
                created_at = r2.GetString(3)
            });
        }

        return new
        {
            run_id = r.GetString(0),
            run_type = r.GetString(1),
            created_at = r.GetString(2),
            outcome = r.GetString(3),
            summary = JsonDocument.Parse(summaryJson).RootElement,
            artifacts
        };
    }
}

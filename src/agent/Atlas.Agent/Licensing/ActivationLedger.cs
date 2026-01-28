using System;
using Microsoft.Data.Sqlite;

namespace Atlas.Agent.Licensing;

/// <summary>
/// Offline activation ledger stored in atlas.db.
/// One row per (license_id, device_id).
/// </summary>
public sealed class ActivationLedger
{
    private readonly string _dbPath;

    public ActivationLedger(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void EnsureSchema()
    {
        using var cn = new SqliteConnection($"Data Source={_dbPath}");
        cn.Open();

        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS license_activations (
  license_id      TEXT NOT NULL,
  device_id       TEXT NOT NULL,
  first_seen_utc  TEXT NOT NULL,
  last_seen_utc   TEXT NOT NULL,
  PRIMARY KEY (license_id, device_id)
);
";
        cmd.ExecuteNonQuery();
    }

    public void Touch(string licenseId, string deviceId, DateTimeOffset nowUtc)
    {
        using var cn = new SqliteConnection($"Data Source={_dbPath}");
        cn.Open();

        using var tx = cn.BeginTransaction();

        // Upsert activation (first_seen preserved, last_seen updated)
        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO license_activations (license_id, device_id, first_seen_utc, last_seen_utc)
VALUES ($lid, $did, $now, $now)
ON CONFLICT(license_id, device_id)
DO UPDATE SET last_seen_utc = excluded.last_seen_utc;
";
            cmd.Parameters.AddWithValue("$lid", licenseId);
            cmd.Parameters.AddWithValue("$did", deviceId);
            cmd.Parameters.AddWithValue("$now", nowUtc.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public int CountDevices(string licenseId)
    {
        using var cn = new SqliteConnection($"Data Source={_dbPath}");
        cn.Open();

        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM license_activations WHERE license_id = $lid;";
        cmd.Parameters.AddWithValue("$lid", licenseId);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
using Npgsql;

namespace Atlas.ActivationAuthority.Activation;

public sealed class PostgresActivationStore : IActivationStore
{
    // pulled from launchSettings.json env ATLAS_DB_CONNECTION
    private readonly string _cs;

    public PostgresActivationStore(IConfiguration cfg)
    {
        _cs = cfg["ATLAS_DB_CONNECTION"] ?? throw new InvalidOperationException("ATLAS_DB_CONNECTION missing");
    }

    public async Task<int> CountDevicesForLicenseAsync(string tenantId, string licenseId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
select count(*)
from atlas_activation_devices
where tenant_id = @tenant_id
  and license_id = @license_id;
";
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("license_id", licenseId);

        var obj = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(obj);
    }

    public async Task RegisterDeviceAsync(
        string tenantId,
        string licenseId,
        string deviceId,
        string hardwareFingerprint,
        string agentVersion,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        // Upsert device
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
insert into atlas_activation_devices(tenant_id, license_id, device_id, hardware_fingerprint, agent_version)
values (@tenant_id, @license_id, @device_id, @hwfp, @agentver)
on conflict (tenant_id, license_id, device_id)
do update set hardware_fingerprint = excluded.hardware_fingerprint,
              agent_version = excluded.agent_version,
              last_seen_utc = now();
";
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("license_id", licenseId);
            cmd.Parameters.AddWithValue("device_id", deviceId);
            cmd.Parameters.AddWithValue("hwfp", hardwareFingerprint ?? "");
            cmd.Parameters.AddWithValue("agentver", agentVersion ?? "");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Audit
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
insert into atlas_activation_audit(tenant_id, license_id, device_id, action)
values (@tenant_id, @license_id, @device_id, 'activation_claim');
";
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("license_id", licenseId);
            cmd.Parameters.AddWithValue("device_id", deviceId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}

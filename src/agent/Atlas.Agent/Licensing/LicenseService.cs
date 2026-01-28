using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Atlas.Agent.Security;

namespace Atlas.Agent.Licensing;

/// <summary>
/// Canonical local/offline licensing service.
/// - Reads atlas.license.json + atlas.license.sig (AppPaths)
/// - Verifies signature locally
/// - Enforces expiration
/// - Applies tier caps (clamp)
/// - Enforces device limit locally (devices.json)
/// </summary>
public sealed class LicenseService
{
    private readonly AppPaths.PathSet _paths;

    private readonly ActivationLedger _ledger;
    public LicenseService(AppPaths.PathSet paths)
    {
        _paths = paths;
        _ledger = new ActivationLedger(paths.DbPath);
        _ledger.EnsureSchema();
    }

    public void Load()
    {
        // Best-effort warm load (no throw).
        try { _ = GetLicenseStatus(DeviceIdentity.GetDeviceId()); }
        catch { }
    }

    public System.Threading.Tasks.Task<LicenseStatus> GetLicenseStatusAsync(string deviceId)
        => System.Threading.Tasks.Task.FromResult(GetLicenseStatus(deviceId));

    private LicenseStatus GetLicenseStatus(string deviceId)
    {
        if (!File.Exists(_paths.LicenseJsonPath) || !File.Exists(_paths.LicenseSigPath))
            return LicenseStatus.Unlicensed("license_files_missing");

        if (!LicenseVerifier.Verify(_paths.LicenseJsonPath, _paths.LicenseSigPath))
            return LicenseStatus.Unlicensed("license_signature_invalid");

        LicensePayload payload;
        try
        {
            var json = File.ReadAllText(_paths.LicenseJsonPath);
            payload = JsonSerializer.Deserialize<LicensePayload>(json, JsonOpts())
                      ?? throw new InvalidOperationException("license_payload_invalid");
        }
        catch
        {
            return LicenseStatus.Unlicensed("license_payload_invalid");
        }

        if (!Enum.TryParse<LicenseTier>(payload.Tier, ignoreCase: true, out var tier))
            return LicenseStatus.Unlicensed("license_tier_invalid");

        if (IsExpired(payload.ExpiresAt))
            return LicenseStatus.Unlicensed("license_expired");

        var requested = Feature.None;
        foreach (var slug in payload.Features ?? Array.Empty<string>())
            requested |= SlugToFeature(slug);

        var capped = LicensePolicy.ApplyCaps(tier, requested, payload.DeviceLimit);

        var status = LicenseStatus.LicensedOk(
    licenseId: payload.LicenseId ?? "",
    customer: payload.Customer ?? "",
    tier: tier,
    features: capped.AllowedFeatures,
    deviceLimit: capped.AllowedDeviceLimit,
    issuedAt: payload.IssuedAt,
    expiresAt: payload.ExpiresAt
);
        // DEVICE_LIMIT_LEDGER_BEGIN
        // Device-limit enforcement (offline ledger)
        // NOTE: runs only after signature verify + tier caps computed.
        if (status.Licensed && !string.IsNullOrWhiteSpace(status.LicenseId) && status.DeviceLimit > 0)
        {
            _ledger.Touch(status.LicenseId!, deviceId, DateTimeOffset.UtcNow);
            var n = _ledger.CountDevices(status.LicenseId!);
            if (n > status.DeviceLimit)
                return LicenseStatus.Denied("device_limit_exceeded: " + n + "/" + status.DeviceLimit, status);
        }
        // DEVICE_LIMIT_LEDGER_END

        return status;
    }

    private static bool IsExpired(DateTimeOffset? expiresAtUtc)
    {
        if (expiresAtUtc is null) return false;
        return expiresAtUtc.Value.UtcDateTime <= DateTime.UtcNow;
    }

    private bool EnforceDeviceLimit(string deviceId, int allowedDeviceLimit)
    {
        if (allowedDeviceLimit <= 0) return false;

        try
        {
            Directory.CreateDirectory(_paths.DataRoot);

            var regPath = Path.Combine(_paths.DataRoot, "devices.json");

            var devices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(regPath))
            {
                try
                {
                    var json = File.ReadAllText(regPath);
                    var arr = JsonSerializer.Deserialize<string[]>(json, JsonOpts()) ?? Array.Empty<string>();
                    foreach (var d in arr)
                        if (!string.IsNullOrWhiteSpace(d))
                            devices.Add(d.Trim());
                }
                catch
                {
                    devices.Clear();
                }
            }

            devices.Add(deviceId);

            if (devices.Count > allowedDeviceLimit)
                return false;

            File.WriteAllText(regPath, JsonSerializer.Serialize(devices.ToArray(), JsonOpts()));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Feature SlugToFeature(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return Feature.None;

        return slug.Trim().ToLowerInvariant() switch
        {
            "winget_scan" => Feature.WingetScan,
            "artifacts" => Feature.Artifacts,
            "rollback" => Feature.Rollback,
            "export_logs" => Feature.ExportLogs,
            "automation" => Feature.Automation,
            _ => Feature.None
        };
    }

    private static JsonSerializerOptions JsonOpts() => new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };
}
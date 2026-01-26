using System.Text.Json;

namespace Atlas.Agent.Licensing;

public sealed class LicenseService
{
    private readonly AppPaths _paths;

    public LicenseService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<object> GetLicenseStatusAsync(string deviceId)
    {
        // Phase 1: local token file (optional). If missing → trial.
        if (!File.Exists(_paths.LicenseTokenPath))
        {
            return new
            {
                state = "trial",
                tier = "tier1_personal",
                max_devices = 1,
                device_id = deviceId,
                enrollment_state = "active",
                expires_at = (string?)null,
                grace_ends_at = (string?)null
            };
        }

        using var fs = File.OpenRead(_paths.LicenseTokenPath);
        var doc = await JsonDocument.ParseAsync(fs);

        // Expected minimal fields (you can harden later)
        var tier = doc.RootElement.GetProperty("tier").GetString() ?? "tier1_personal";
        var maxDevices = doc.RootElement.GetProperty("max_devices").GetInt32();
        var state = doc.RootElement.TryGetProperty("state", out var s) ? (s.GetString() ?? "active") : "active";
        var expiresAt = doc.RootElement.TryGetProperty("expires_at", out var e) ? e.GetString() : null;
        var graceEndsAt = doc.RootElement.TryGetProperty("grace_ends_at", out var g) ? g.GetString() : null;

        return new
        {
            state,
            tier,
            max_devices = maxDevices,
            device_id = deviceId,
            enrollment_state = "active",
            expires_at = expiresAt,
            grace_ends_at = graceEndsAt
        };
    }
}

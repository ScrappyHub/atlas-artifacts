using System;
using System.IO;
using System.Text.Json;

namespace Atlas.Agent.Licensing;

public sealed class LicenseService
{
    public License Current { get; private set; } = CreateDefault();

    public bool LoadFromDisk(string jsonPath, string sigPath)
    {
        if (!File.Exists(jsonPath) || !File.Exists(sigPath))
        {
            Current = CreateDefault();
            return false;
        }

        // Verify signature first (never trust JSON without sig)
        if (!LicenseVerifier.Verify(jsonPath, sigPath))
        {
            Current = CreateDefault();
            return false;
        }

        var json = File.ReadAllText(jsonPath);
        var payload = JsonSerializer.Deserialize<LicensePayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (payload is null)
        {
            Current = CreateDefault();
            return false;
        }

        var tier = TierPolicy.ParseTier(payload.Tier);
        var requestedFeatures = TierPolicy.ParseFeatures(payload.Features);
        var (features, devices) = TierPolicy.ApplyPolicy(tier, payload.DeviceLimit, requestedFeatures);

        // Expiry gate
        var now = DateTimeOffset.UtcNow;
        if (TierPolicy.IsExpired(payload.ExpiresAt, now))
        {
            Current = CreateDefault();
            return false;
        }

        Current = new License(
            LicenseId: payload.LicenseId ?? "lic_free",
            Customer: payload.Customer ?? "unlicensed",
            Tier: tier,
            DeviceLimit: devices,
            Features: features,
            IssuedAt: payload.IssuedAt == default ? now : payload.IssuedAt,
            ExpiresAt: payload.ExpiresAt
        );

        return true;
    }

    private static License CreateDefault()
    {
        var now = DateTimeOffset.UtcNow;
        var (features, devices) = TierPolicy.ApplyPolicy(Tier.Free, 1, Feature.WingetScan);

        return new License(
            LicenseId: "lic_free",
            Customer: "unlicensed",
            Tier: Tier.Free,
            DeviceLimit: devices,
            Features: features,
            IssuedAt: now,
            ExpiresAt: null
        );
    }
}

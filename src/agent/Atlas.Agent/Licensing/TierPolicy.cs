using System;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Agent.Licensing;

public sealed record TierEntitlements(
    Tier Tier,
    int MaxDevices,
    Feature Features
);

public static class TierPolicy
{
    // Canonical tier contract.
    // If you change this, you must change docs/licensing.md and schemas/license.schema.json in the same commit.
    public static readonly IReadOnlyDictionary<Tier, TierEntitlements> Entitlements =
        new Dictionary<Tier, TierEntitlements>
        {
            // FREE: keep intentionally limited but real
            [Tier.Free] = new TierEntitlements(
                Tier: Tier.Free,
                MaxDevices: 1,
                Features: Feature.WingetScan
            ),

            // PERSONAL: solo operator
            [Tier.Personal] = new TierEntitlements(
                Tier: Tier.Personal,
                MaxDevices: 1,
                Features: Feature.WingetScan | Feature.Artifacts
            ),

            // PRO: power user / small shop
            [Tier.Pro] = new TierEntitlements(
                Tier: Tier.Pro,
                MaxDevices: 5,
                Features: Feature.WingetScan | Feature.Artifacts | Feature.ExportLogs
            ),

            // BUSINESS: real operations / rollback becomes core value
            [Tier.Business] = new TierEntitlements(
                Tier: Tier.Business,
                MaxDevices: 25,
                Features: Feature.WingetScan | Feature.Artifacts | Feature.ExportLogs | Feature.Rollback
            ),

            // ENTERPRISE: large org / automation allowed
            [Tier.Enterprise] = new TierEntitlements(
                Tier: Tier.Enterprise,
                MaxDevices: 250,
                Features: Feature.WingetScan | Feature.Artifacts | Feature.ExportLogs | Feature.Rollback | Feature.Automation
            ),
        };

    public static Tier ParseTier(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier)) return Tier.Free;

        return tier.Trim().ToLowerInvariant() switch
        {
            "free" => Tier.Free,
            "personal" => Tier.Personal,
            "pro" => Tier.Pro,
            "business" => Tier.Business,
            "enterprise" => Tier.Enterprise,
            _ => Tier.Free
        };
    }

    // Convert ["winget_scan","artifacts"] -> Feature flags
    public static Feature ParseFeatures(IEnumerable<string>? features)
    {
        if (features is null) return Feature.None;

        Feature f = Feature.None;
        foreach (var s in features)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;

            f |= s.Trim().ToLowerInvariant() switch
            {
                "winget_scan" => Feature.WingetScan,
                "artifacts" => Feature.Artifacts,
                "rollback" => Feature.Rollback,
                "export_logs" => Feature.ExportLogs,
                "automation" => Feature.Automation,
                _ => Feature.None
            };
        }
        return f;
    }

    // Canonical enforcement: Tier sets the ceiling.
    // Payload can request less than tier, but never more.
    public static (Feature AllowedFeatures, int AllowedDevices) ApplyPolicy(Tier tier, int requestedDevices, Feature requestedFeatures)
    {
        if (!Entitlements.TryGetValue(tier, out var ent))
            ent = Entitlements[Tier.Free];

        // Clamp device count into [1..MaxDevices]
        var devices = requestedDevices <= 0 ? 1 : requestedDevices;
        devices = Math.Min(devices, ent.MaxDevices);

        // Clamp features to the tier’s allowed set
        var features = requestedFeatures & ent.Features;

        return (features, devices);
    }

    public static bool IsExpired(DateTimeOffset? expiresAt, DateTimeOffset nowUtc)
        => expiresAt.HasValue && expiresAt.Value <= nowUtc;

    public static string Describe(Tier tier)
    {
        if (!Entitlements.TryGetValue(tier, out var ent))
            ent = Entitlements[Tier.Free];

        return $"{tier} (maxDevices={ent.MaxDevices}, features={ent.Features})";
    }
}

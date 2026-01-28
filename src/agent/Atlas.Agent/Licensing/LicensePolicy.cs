using System;

namespace Atlas.Agent.Licensing;

/// <summary>
/// Canonical tier caps (non-negotiable).
/// Tier defines an upper bound; payload may request LESS but NEVER MORE.
/// </summary>
public static class LicensePolicy
{
    public static (Feature AllowedFeatures, int AllowedDeviceLimit) ApplyCaps(
        LicenseTier tier,
        Feature requestedFeatures,
        int requestedDeviceLimit
    )
    {
        var (tierFeatures, tierMaxDevices) = TierCaps(tier);

        var allowedFeatures = requestedFeatures & tierFeatures;

        var req = requestedDeviceLimit < 0 ? 0 : requestedDeviceLimit;
        var allowedDeviceLimit = Math.Min(req, tierMaxDevices);

        return (allowedFeatures, allowedDeviceLimit);
    }

    private static (Feature TierFeatures, int TierMaxDevices) TierCaps(LicenseTier tier)
    {
        return tier switch
        {
            LicenseTier.Free =>
                (Feature.WingetScan, 1),

            LicenseTier.Personal =>
                (Feature.WingetScan | Feature.Artifacts, 1),

            LicenseTier.Pro =>
                (Feature.WingetScan | Feature.Artifacts | Feature.ExportLogs, 5),

            LicenseTier.Business =>
                (Feature.WingetScan | Feature.Artifacts | Feature.ExportLogs | Feature.Rollback, 25),

            LicenseTier.Enterprise =>
                (Feature.WingetScan | Feature.Artifacts | Feature.ExportLogs | Feature.Rollback | Feature.Automation, 250),

            _ =>
                (Feature.None, 0)
        };
    }
}
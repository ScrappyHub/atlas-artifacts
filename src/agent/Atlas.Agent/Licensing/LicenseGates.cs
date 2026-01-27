using System;

namespace Atlas.Agent.Licensing;

/// <summary>
/// Canonical license enforcement gates.
/// - Router/handlers must call these for every gated feature.
/// - No UI/checkbox "honor system".
/// </summary>
public static class LicenseGates
{
    public static void RequireFeature(Feature licensedFeatures, Feature required, string featureName)
    {
        if ((licensedFeatures & required) != required)
            throw new LicenseDeniedException($"{featureName} not licensed.");
    }

    public static void RequireNotExpired(DateTimeOffset nowUtc, DateTimeOffset? expiresAtUtc)
    {
        if (expiresAtUtc is null) return;
        if (expiresAtUtc.Value <= nowUtc)
            throw new LicenseDeniedException("License expired.");
    }
}

public sealed class LicenseDeniedException : Exception
{
    public LicenseDeniedException(string message) : base(message) { }
}

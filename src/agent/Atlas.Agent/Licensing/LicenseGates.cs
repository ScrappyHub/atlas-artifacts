using System;

namespace Atlas.Agent.Licensing;

/// <summary>
/// Canonical runtime enforcement helpers.
/// These throw on denial so router handlers can catch and return an IPC failure.
/// </summary>
public static class LicenseGates
{
    public static void RequireLicensed(LicenseStatus status)
    {
        if (!status.Licensed)
            throw new LicenseDeniedException($"unlicensed: {status.Reason}");
    }

    public static void RequireFeature(Feature effectiveFeatures, Feature required, string name)
    {
        if ((effectiveFeatures & required) != required)
            throw new LicenseDeniedException($"feature_not_licensed: {name}");
    }
}
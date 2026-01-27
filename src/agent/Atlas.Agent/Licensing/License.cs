using System;

namespace Atlas.Agent.Licensing;

public sealed record License(
    string LicenseId,
    string Customer,
    string Tier,
    int DeviceLimit,
    Feature Features,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt
)
{
    public bool HasFeature(Feature f) => (Features & f) == f;

    public bool IsExpired(DateTimeOffset nowUtc)
        => ExpiresAt.HasValue && nowUtc > ExpiresAt.Value;

    public static License Unlicensed() => new(
        LicenseId: "unlicensed",
        Customer: "unlicensed",
        Tier: "none",
        DeviceLimit: 1,
        Features: Feature.None,
        IssuedAt: DateTimeOffset.UnixEpoch,
        ExpiresAt: null
    );
}

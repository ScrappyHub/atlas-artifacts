using System;

namespace Atlas.Agent.Licensing;

public sealed record License(
    string LicenseId,
    string Customer,
    Tier Tier,
    int DeviceLimit,
    Feature Features,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt
)
{
    public bool Has(Feature f) => (Features & f) == f;
}

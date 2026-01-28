using System;

namespace Atlas.Agent.Licensing;

/// <summary>
/// Effective, clamped license after:
/// - signature verify
/// - expiration check
/// - tier caps applied
/// </summary>
public sealed record License(
    string LicenseId,
    string Customer,
    LicenseTier Tier,
    int DeviceLimit,
    Feature Features,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt
);
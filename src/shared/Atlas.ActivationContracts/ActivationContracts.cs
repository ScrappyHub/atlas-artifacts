using System;

namespace Atlas.ActivationContracts;

// Canonical identity
public sealed record ActivationClaimRequest(
    string TenantId,
    string CustomerId,
    string LicenseId,
    string DeviceId,
    string HardwareFingerprint,
    string AgentVersion
);

public sealed record ActivationClaimResponse(
    bool Ok,
    string Message,
    ActivationGrant? Grant
);

// Short-lived activation grant (JWT-like in future)
public sealed record ActivationGrant(
    string TenantId,
    string CustomerId,
    string LicenseId,
    string DeviceId,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Signature // server-signed, validated by agent (future)
);
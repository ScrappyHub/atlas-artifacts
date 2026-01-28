using System;
using System.Text.Json.Serialization;

namespace Atlas.Agent.Licensing;

/// <summary>
/// Raw JSON payload model for atlas.license.json (canonical contract).
/// Property names are snake_case to match disk payload exactly.
/// </summary>
public sealed record LicensePayload(
    [property: JsonPropertyName("license_id")] string LicenseId,
    [property: JsonPropertyName("customer")] string Customer,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("device_limit")] int DeviceLimit,
    [property: JsonPropertyName("features")] string[] Features,
    [property: JsonPropertyName("issued_at")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt
);
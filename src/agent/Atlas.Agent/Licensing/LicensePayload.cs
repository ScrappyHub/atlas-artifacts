using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Atlas.Agent.Licensing;

// This models the raw JSON contract (snake_case keys).
public sealed record LicensePayload(
    [property: JsonPropertyName("license_id")] string LicenseId,
    [property: JsonPropertyName("customer")] string Customer,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("device_limit")] int DeviceLimit,
    [property: JsonPropertyName("features")] IReadOnlyList<string> Features,
    [property: JsonPropertyName("issued_at")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt
);

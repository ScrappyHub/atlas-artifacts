using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Atlas.Agent.Licensing;

public sealed class LicensePayload
{
    [JsonPropertyName("license_id")]
    public string? LicenseId { get; set; }

    [JsonPropertyName("customer")]
    public string? Customer { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("device_limit")]
    public int DeviceLimit { get; set; }

    [JsonPropertyName("features")]
    public List<string>? Features { get; set; }

    [JsonPropertyName("issued_at")]
    public DateTimeOffset IssuedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }
}

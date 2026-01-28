using System.Collections.Concurrent;

namespace Atlas.ActivationAuthority.Activation;

public sealed class InMemoryActivationStore : IActivationStore
{
    // key: tenant|license|device
    private readonly ConcurrentDictionary<string, DeviceRow> _devices = new();

    public Task<int> CountDevicesForLicenseAsync(string tenantId, string licenseId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var prefix = $"{tenantId}|{licenseId}|";
        var count = _devices.Keys.Count(k => k.StartsWith(prefix, StringComparison.Ordinal));
        return Task.FromResult(count);
    }

    public Task RegisterDeviceAsync(
        string tenantId,
        string licenseId,
        string deviceId,
        string hardwareFingerprint,
        string agentVersion,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = $"{tenantId}|{licenseId}|{deviceId}";
        _devices[key] = new DeviceRow(
            TenantId: tenantId,
            LicenseId: licenseId,
            DeviceId: deviceId,
            HardwareFingerprint: hardwareFingerprint ?? "",
            AgentVersion: agentVersion ?? "",
            CreatedAtUtc: DateTimeOffset.UtcNow
        );
        return Task.CompletedTask;
    }

    private sealed record DeviceRow(
        string TenantId,
        string LicenseId,
        string DeviceId,
        string HardwareFingerprint,
        string AgentVersion,
        DateTimeOffset CreatedAtUtc
    );
}

namespace Atlas.ActivationAuthority.Activation;

public interface IActivationStore
{
    Task<int> CountDevicesForLicenseAsync(string tenantId, string licenseId, CancellationToken ct);

    Task RegisterDeviceAsync(
        string tenantId,
        string licenseId,
        string deviceId,
        string hardwareFingerprint,
        string agentVersion,
        CancellationToken ct);
}

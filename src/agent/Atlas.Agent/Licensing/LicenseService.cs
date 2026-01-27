namespace Atlas.Agent.Licensing;

public sealed class LicenseService
{
    public bool IsLicensed { get; private set; }
    public string? LicenseId { get; private set; }

    public void Load()
    {
        IsLicensed = false;
        LicenseId = null;
    }

    public void SetLicensed(string licenseId)
    {
        IsLicensed = true;
        LicenseId = licenseId;
    }
}
namespace Atlas.Agent.Licensing;

public sealed class LicenseDeniedException : Exception
{
    public LicenseDeniedException(string message) : base(message) { }
}

public static class LicenseGates
{
    public static void RequireFeature(Feature licensedFeatures, Feature required, string featureName)
    {
        if ((licensedFeatures & required) != required)
            throw new LicenseDeniedException($"{featureName} not licensed.");
    }

    public static void RequireLicensed(bool isLicensed, string messageIfNotLicensed)
    {
        if (!isLicensed)
            throw new LicenseDeniedException(messageIfNotLicensed);
    }
}

using System;

namespace Atlas.Agent.Licensing;

public static class LicenseGates
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new LicenseDeniedException(message);
    }

    public static void RequireFeature(Feature enabled, Feature required, string actionName)
    {
        if ((enabled & required) != required)
            throw new LicenseDeniedException($"{actionName} not licensed. Required={required}.");
    }
}

public sealed class LicenseDeniedException : Exception
{
    public LicenseDeniedException(string message) : base(message) { }
}

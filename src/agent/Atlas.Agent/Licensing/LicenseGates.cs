namespace Atlas.Agent.Licensing;

public static class LicenseGates
{
    public static IpcResponse Require(License lic, Feature f, string actionName)
    {
        if (!lic.Has(f))
            return IpcResponse.Fail($"{actionName} not licensed. Required={f}. Tier={lic.Tier}.");

        return IpcResponse.Ok();
    }
}

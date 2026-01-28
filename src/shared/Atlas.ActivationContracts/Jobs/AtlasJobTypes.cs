namespace Atlas.ActivationContracts.Jobs;

/// <summary>
/// Wire-stable job type ids (do not renumber).
/// </summary>
public static class AtlasJobTypes
{
    // Existing types (keep your existing mapping; leave Update as-is if already defined elsewhere)
    public const int Update = 0;

    // Profiles (restore points)
    public const int ProfileExport = 100;
    public const int ProfileRestore = 101;
    public const int ProfileVerify = 102;
}
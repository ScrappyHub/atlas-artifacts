namespace Atlas.Agent.ProfileEngine;

public static class ProfilePaths
{
    public static string RootDir()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // profiles/{tenantId}/{deviceId}/{profileId}/...
        return Path.Combine(baseDir, "Atlas", "profiles");
    }

    public static string ProfileDir(string tenantId, string deviceId, string profileId)
        => Path.Combine(RootDir(), tenantId, deviceId, profileId);
}
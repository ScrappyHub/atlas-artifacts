<<<<<<< HEAD
namespace Atlas.Agent;

public sealed record AppPaths(
    string DataDir,
    string DbPath,
    string ArtifactsDir,
    string DeviceSaltPath,
    string LicenseTokenPath
)
{
    public static AppPaths Resolve()
    {
        // Keep Phase 1 local and simple. Later you can move to ProgramData for a true service install.
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtlasUpdate"
        );

        return new AppPaths(
            DataDir: baseDir,
            DbPath: Path.Combine(baseDir, "atlas.sqlite"),
            ArtifactsDir: Path.Combine(baseDir, "artifacts"),
            DeviceSaltPath: Path.Combine(baseDir, "device.salt"),
            LicenseTokenPath: Path.Combine(baseDir, "license.token.json")
        );
    }
}
=======
namespace Atlas.Agent;

public sealed record AppPaths(
    string DataDir,
    string DbPath,
    string ArtifactsDir,
    string DeviceSaltPath,
    string LicenseTokenPath
)
{
    public static AppPaths Resolve()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ""AtlasUpdate""
        );

        return new AppPaths(
            DataDir: baseDir,
            DbPath: Path.Combine(baseDir, ""atlas.sqlite""),
            ArtifactsDir: Path.Combine(baseDir, ""artifacts""),
            DeviceSaltPath: Path.Combine(baseDir, ""device.salt""),
            LicenseTokenPath: Path.Combine(baseDir, ""license.token.json"")
        );
    }
}
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

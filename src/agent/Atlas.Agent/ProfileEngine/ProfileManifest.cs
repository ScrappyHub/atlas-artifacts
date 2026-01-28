namespace Atlas.Agent.ProfileEngine;

// Internal manifest model (what we write deterministically with CanonicalJson)
public sealed class ProfileManifest
{
    public string schema { get; set; } = "atlas.profile.manifest.v1";

    public string tenantId { get; set; } = "";
    public string deviceId { get; set; } = "";
    public string licenseId { get; set; } = "";
    public string profileId { get; set; } = "";
    public string createdAtUtc { get; set; } = "";

    public AgentInfo agent { get; set; } = new();

    public string scope { get; set; } = "files";
    public string sourceRoot { get; set; } = "";

    public EncryptionInfo encryption { get; set; } = new();

    public List<FileEntry> files { get; set; } = new();

    public sealed class AgentInfo
    {
        public string version { get; set; } = "";
        public string hardwareFingerprint { get; set; } = "";
    }

    public sealed class EncryptionInfo
    {
        public string mode { get; set; } = "none"; // none | aesgcm
        public string aad { get; set; } = "";
        public object? kdf { get; set; } = null;
    }

    public sealed class FileEntry
    {
        public string path { get; set; } = "";
        public long size { get; set; }
        public string sha256 { get; set; } = "";
        public bool skipped { get; set; } = false;
        public string? reason { get; set; } = null;
    }
}
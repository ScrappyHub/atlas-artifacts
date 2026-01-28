using System.Text.Json.Serialization;

namespace Atlas.ActivationContracts.Profiles;

// -------------------- EXPORT (server -> agent payload) --------------------
public sealed record ProfileExportPayload(
    string Schema,
    string ProfileId,
    string TenantId,
    string DeviceId,
    string LicenseId,
    string Scope,
    string SourceRoot,
    string[] Include,
    string[] Exclude,
    ProfileEncryptionSpec Encryption,
    ProfilePolicySpec Policy
);

// -------------------- EXPORT RESULT (agent -> server resultJson) --------------------
public sealed record ProfileExportResult(
    string Schema,
    string ProfileId,
    ProfileArtifacts Artifacts,
    ProfileHashes Hashes,
    string PublicKeyB64,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc
);

public sealed record ProfileArtifacts(
    string Manifest,
    string ManifestHash,
    string Signature,
    string ShaList,
    string? Bundle,
    string? BundleHash
);

public sealed record ProfileHashes(
    string ManifestSha256Hex,
    string? BundleSha256Hex
);

// -------------------- RESTORE (server -> agent payload) --------------------
public sealed record ProfileRestorePayload(
    string Schema,
    string ProfileId,
    ProfileExpectedBinding Expected,
    string ProfileDir,     // absolute path to {profiles/.../{profileId}} on agent
    bool VerifyOnly
);

public sealed record ProfileExpectedBinding(
    string TenantId,
    string DeviceId,
    string LicenseId,
    string ManifestSha256Hex
);

// -------------------- VERIFY RESULT (agent -> server resultJson) --------------------
public sealed record ProfileVerifyResult(
    string Schema,
    string ProfileId,
    string Status,   // verified_ok | verified_partial | verified_failed
    string Code,     // ok | mismatch | signature_invalid | file_missing | etc
    string[] Notes
);

// -------------------- COMMON --------------------
public sealed record ProfileEncryptionSpec(
    string Mode,              // none | aesgcm
    string? PassphraseHint,   // optional hint shown to operator
    string? SaltB64           // optional (future); v1 can be null
);

public sealed record ProfilePolicySpec(
    string[] Allow,
    string[] Deny,
    long? MaxBytes
);
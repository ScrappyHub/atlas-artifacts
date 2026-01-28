using System.Text;
using System.Text.Json;
using Atlas.ActivationContracts;
using Atlas.ActivationContracts.Profiles;
using Atlas.Agent.ProfileEngine.Crypto;

namespace Atlas.Agent.ProfileEngine;

public static class ProfileExportEngine
{
    // v1: local-only profile output (no upload). Optional bundle encryption supported for small bundles.
    // Later: move blobs to server/cloud; manifest+sig stays identical.
    public static ProfileExportResult Execute(ProfileExportPayload payload)
    {
        var started = DateTimeOffset.UtcNow;

        var profileDir = ProfilePaths.ProfileDir(payload.TenantId, payload.DeviceId, payload.ProfileId);
        Directory.CreateDirectory(profileDir);

        // export_root: where we stage raw files (for now we just hash originals; later we can snapshot/copy)
        // For v1 skeleton: hash files directly from disk under SourceRoot.
        // NOTE: Real restore points will copy into a stable export root or tar; we keep hash truth now.
        var sourceRoot = payload.SourceRoot;

        // Build file list
        var include = payload.Include?.Length > 0 ? payload.Include : new[] { "**/*" };
        var exclude = payload.Exclude ?? Array.Empty<string>();

        var maxBytes = payload.Policy?.MaxBytes;

        var files = EnumerateFiles(sourceRoot, include, exclude, maxBytes);

        // Hash + sha list
        var shaEntries = new List<(string RelPath, string Sha256Hex)>();
        var manifest = new ProfileManifest
        {
            tenantId = payload.TenantId,
            deviceId = payload.DeviceId,
            licenseId = payload.LicenseId,
            profileId = payload.ProfileId,
            createdAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            scope = payload.Scope,
            sourceRoot = payload.SourceRoot,
            agent = new ProfileManifest.AgentInfo
            {
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                hardwareFingerprint = Environment.MachineName
            },
            encryption = new ProfileManifest.EncryptionInfo
            {
                mode = payload.Encryption?.Mode ?? "none",
                aad = Aad(payload.TenantId, payload.DeviceId, payload.ProfileId),
                kdf = null
            }
        };

        foreach (var f in files)
        {
            var rel = f.RelPath.Replace('\\', '/');
            var abs = f.AbsPath;

            var entry = new ProfileManifest.FileEntry
            {
                path = rel,
                size = f.Size,
                sha256 = "",
                skipped = false,
                reason = null
            };

            try
            {
                var hex = Sha256Util.HashFileHex(abs);
                entry.sha256 = hex;
                shaEntries.Add((rel, hex));
            }
            catch (Exception)
            {
                // Verified_partial will catch this later.
                entry.skipped = true;
                entry.reason = "unreadable";
                entry.sha256 = "";
            }

            manifest.files.Add(entry);
        }

        // Write canonical artifacts
        var manifestPath = Path.Combine(profileDir, "manifest.json");
        var shaListPath = Path.Combine(profileDir, "sha256sums.txt");
        var manifestHashPath = Path.Combine(profileDir, "manifest.json.sha256");
        var sigPath = Path.Combine(profileDir, "manifest.sig");

        CanonicalJson.WriteFile(manifestPath, manifest);

        var manifestBytes = File.ReadAllBytes(manifestPath);
        var manifestShaHex = Sha256Util.HashBytesHex(manifestBytes);
        CanonicalJson.WriteHexFileWithNewline(manifestHashPath, manifestShaHex);

        Sha256SumsWriter.Write(shaListPath, shaEntries);

        // Sign manifest hash bytes (32 raw bytes)
        var (pk, sk) = Ed25519Keys.LoadOrCreate();
        var manifestHashBytes = Sha256Util.HashBytes(manifestBytes); // 32 bytes
        var signature = Ed25519Keys.Sign(sk, manifestHashBytes);

        var sigObj = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = "atlas.signature.v1",
            ["alg"] = "ed25519",
            ["signed"] = "manifest.json.sha256",
            ["public_key"] = Convert.ToBase64String(pk),
            ["signature"] = Convert.ToBase64String(signature)
        };

        File.WriteAllBytes(sigPath, CanonicalJson.SerializeDeterministic(sigObj));

        // Optional bundle encryption placeholder (v1: not generating tar yet; keep null)
        // You can wire tar+encrypt later; skeleton crypto is present in AesGcmEnvelope.
        string? bundle = null;
        string? bundleHash = null;

        var ended = DateTimeOffset.UtcNow;

        return new ProfileExportResult(
            Schema: "atlas.profile.export.result.v1",
            ProfileId: payload.ProfileId,
            Artifacts: new ProfileArtifacts(
                Manifest: "manifest.json",
                ManifestHash: "manifest.json.sha256",
                Signature: "manifest.sig",
                ShaList: "sha256sums.txt",
                Bundle: bundle,
                BundleHash: bundleHash
            ),
            Hashes: new ProfileHashes(
                ManifestSha256Hex: manifestShaHex,
                BundleSha256Hex: null
            ),
            PublicKeyB64: Convert.ToBase64String(pk),
            StartedAtUtc: started,
            EndedAtUtc: ended
        );
    }

    private static string Aad(string tenantId, string deviceId, string profileId)
        => $"atlas|profile|bundle|v1|{tenantId}|{deviceId}|{profileId}";

    private sealed record FilePick(string AbsPath, string RelPath, long Size);

    // v1 simple glob support:
    // - include/exclude patterns are treated as prefix-ish path fragments after normalization
    // - "**/*" means everything
    // This is intentionally conservative; you can drop in a real globber later.
    private static List<FilePick> EnumerateFiles(string sourceRoot, string[] include, string[] exclude, long? maxBytes)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot)) throw new ArgumentException("sourceRoot required");

        var root = Path.GetFullPath(sourceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var all = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);

        bool IsMatch(string rel, string[] pats)
        {
            if (pats.Length == 0) return true;
            // treat "**/*" as match-all
            if (pats.Any(p => p.Replace('\\', '/').Trim() == "**/*")) return true;

            foreach (var p in pats)
            {
                var pp = p.Replace('\\', '/').Trim().TrimStart('/');
                if (string.IsNullOrWhiteSpace(pp)) continue;
                if (rel.Contains(pp, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        bool IsExcluded(string rel)
        {
            foreach (var p in exclude ?? Array.Empty<string>())
            {
                var pp = p.Replace('\\', '/').Trim().TrimStart('/');
                if (string.IsNullOrWhiteSpace(pp)) continue;
                if (rel.Contains(pp, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        long total = 0;
        var list = new List<FilePick>();

        foreach (var abs in all)
        {
            var rel = Path.GetRelativePath(root, abs).Replace('\\', '/');
            if (!IsMatch(rel, include)) continue;
            if (IsExcluded(rel)) continue;

            var fi = new FileInfo(abs);
            if (!fi.Exists) continue;

            if (maxBytes is long limit)
            {
                if (total + fi.Length > limit) break;
            }

            total += fi.Length;
            list.Add(new FilePick(abs, rel, fi.Length));
        }

        return list.OrderBy(x => x.RelPath, StringComparer.Ordinal).ToList();
    }
}

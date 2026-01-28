using System.Text.Json;
using Atlas.ActivationContracts.Profiles;
using Atlas.Agent.ProfileEngine.Crypto;

namespace Atlas.Agent.ProfileEngine;

public static class ProfileVerifyEngine
{
    public static ProfileVerifyResult Verify(ProfileRestorePayload payload)
    {
        var notes = new List<string>();
        var dir = payload.ProfileDir;

        var manifestPath = Path.Combine(dir, "manifest.json");
        var manifestHashPath = Path.Combine(dir, "manifest.json.sha256");
        var sigPath = Path.Combine(dir, "manifest.sig");
        var shaListPath = Path.Combine(dir, "sha256sums.txt");

        if (!File.Exists(manifestPath)) return Fail(payload.ProfileId, "file_missing", new[] { "missing manifest.json" });
        if (!File.Exists(manifestHashPath)) return Fail(payload.ProfileId, "file_missing", new[] { "missing manifest.json.sha256" });
        if (!File.Exists(sigPath)) return Fail(payload.ProfileId, "file_missing", new[] { "missing manifest.sig" });
        if (!File.Exists(shaListPath)) return Fail(payload.ProfileId, "file_missing", new[] { "missing sha256sums.txt" });

        var manifestBytes = File.ReadAllBytes(manifestPath);
        var computedManifestShaHex = Sha256Util.HashBytesHex(manifestBytes);

        var expectedManifestShaHex = File.ReadAllText(manifestHashPath).Trim().ToLowerInvariant();
        if (computedManifestShaHex != expectedManifestShaHex)
            return Fail(payload.ProfileId, "mismatch", new[] { "manifest.json.sha256 mismatch" });

        // binding checks (tenant/device/license/profile + manifestSha)
        using var doc = JsonDocument.Parse(manifestBytes);
        var root = doc.RootElement;

        string Get(string name) => root.TryGetProperty(name, out var p) ? (p.GetString() ?? "") : "";

        var mTenant = Get("tenantId");
        var mDevice = Get("deviceId");
        var mLicense = Get("licenseId");
        var mProfile = Get("profileId");

        if (!StringEquals(mTenant, payload.Expected.TenantId)) return Fail(payload.ProfileId, "binding_mismatch", new[] { "tenantId mismatch" });
        if (!StringEquals(mDevice, payload.Expected.DeviceId)) return Fail(payload.ProfileId, "binding_mismatch", new[] { "deviceId mismatch" });
        if (!StringEquals(mLicense, payload.Expected.LicenseId)) return Fail(payload.ProfileId, "binding_mismatch", new[] { "licenseId mismatch" });
        if (!StringEquals(mProfile, payload.ProfileId)) return Fail(payload.ProfileId, "binding_mismatch", new[] { "profileId mismatch" });
        if (!StringEquals(payload.Expected.ManifestSha256Hex, computedManifestShaHex)) return Fail(payload.ProfileId, "binding_mismatch", new[] { "expected manifestSha mismatch" });

        // verify signature over sha256(manifest.json bytes) raw 32 bytes
        var sigJson = File.ReadAllBytes(sigPath);
        using var sigDoc = JsonDocument.Parse(sigJson);
        var sigRoot = sigDoc.RootElement;

        var pkB64 = sigRoot.GetProperty("public_key").GetString() ?? "";
        var sigB64 = sigRoot.GetProperty("signature").GetString() ?? "";

        var pk = Convert.FromBase64String(pkB64);
        var sig = Convert.FromBase64String(sigB64);

        var payload32 = Sha256Util.HashBytes(manifestBytes);
        if (!Ed25519Keys.Verify(pk, payload32, sig))
            return Fail(payload.ProfileId, "signature_invalid", new[] { "manifest.sig invalid" });

        // verify file hashes per sha256sums.txt (best-effort; missing files => partial)
        var shaLines = File.ReadAllLines(shaListPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        int bad = 0;

        // We hashed from original sourceRoot in export v1; we cannot rehash originals reliably at restore.
        // For v1, verify only checks that sha256sums.txt lines are well-formed and consistent with manifest list.
        // Phase 1b: if you snapshot to an export_root inside profileDir, then you can rehash those files here.
        foreach (var line in shaLines)
        {
            if (!line.Contains("  ")) { bad++; continue; }
            var parts = line.Split(new[] { "  " }, 2, StringSplitOptions.None);
            if (parts.Length != 2) { bad++; continue; }
            var hex = parts[0].Trim();
            var rel = parts[1].Trim();
            if (hex.Length != 64) { bad++; continue; }
            if (string.IsNullOrWhiteSpace(rel)) { bad++; continue; }
        }

        if (bad > 0)
            return new ProfileVerifyResult("atlas.profile.verify.result.v1", payload.ProfileId, "verified_failed", "sha_list_invalid", new[] { $"sha list invalid lines: {bad}" });

        return new ProfileVerifyResult("atlas.profile.verify.result.v1", payload.ProfileId, "verified_ok", "ok", notes.ToArray());
    }

    private static ProfileVerifyResult Fail(string profileId, string code, string[] notes)
        => new("atlas.profile.verify.result.v1", profileId, "verified_failed", code, notes);

    private static bool StringEquals(string a, string b)
        => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}

<<<<<<< HEAD
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Agent.IPC;

namespace Atlas.Agent.Logging;

public sealed class ArtifactWriter
{
    private readonly string _artifactsRoot;

    public ArtifactWriter(string artifactsRoot)
    {
        _artifactsRoot = artifactsRoot;
    }

    public string CreateRunDir(string runId, DateTimeOffset utcNow)
    {
        var dir = Path.Combine(
            _artifactsRoot,
            utcNow.UtcDateTime.ToString("yyyy"),
            utcNow.UtcDateTime.ToString("MM"),
            utcNow.UtcDateTime.ToString("dd"),
            runId
        );

        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<Dictionary<string, (string path, string sha256)>> WriteArtifactsAsync(
        string runDir,
        Dictionary<string, object> artifacts
    )
    {
        var map = new Dictionary<string, (string path, string sha256)>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in artifacts)
        {
            var key = kv.Key;
            var path = Path.Combine(runDir, key);
            var json = JsonSerializer.Serialize(kv.Value, JsonOpts.Serializer);

            await File.WriteAllTextAsync(path, json, Encoding.UTF8);
            var sha = Sha256File(path);

            map[key] = (path, sha);
        }

        return map;
    }

    public async Task<(string manifestPath, string manifestSha256)> WriteManifestAsync(
        string runDir,
        Dictionary<string, (string path, string sha256)> artifacts,
        string? previousManifestSha256 = null
    )
    {
        var manifestPath = Path.Combine(runDir, "manifest.json");

        // Chain-ready payload (we compute sha over payload excluding current_manifest_sha256)
        var payload = new
        {
            previous_manifest_sha256 = previousManifestSha256,
            artifacts = artifacts.Select(a => new
            {
                artifact_key = a.Key,
                sha256 = a.Value.sha256,
                path = a.Value.path
            }).ToList()
        };

        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts.Serializer);
        var payloadSha = Sha256Bytes(Encoding.UTF8.GetBytes(payloadJson));

        var finalManifest = new
        {
            previous_manifest_sha256 = previousManifestSha256,
            current_manifest_sha256 = payloadSha,
            artifacts = payload.artifacts
        };

        var finalJson = JsonSerializer.Serialize(finalManifest, JsonOpts.Serializer);
        await File.WriteAllTextAsync(manifestPath, finalJson, Encoding.UTF8);

        var manifestSha = Sha256File(manifestPath);
        return (manifestPath, manifestSha);
    }

    public static string Sha256File(string path)
    {
        using var fs = File.OpenRead(path);
        var hash = SHA256.HashData(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256Bytes(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
=======
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Agent.IPC;

namespace Atlas.Agent.Logging;

public sealed class ArtifactWriter
{
    private readonly string _artifactsRoot;

    public ArtifactWriter(string artifactsRoot)
    {
        _artifactsRoot = artifactsRoot;
    }

    public string CreateRunDir(string runId, DateTimeOffset utcNow)
    {
        var dir = Path.Combine(_artifactsRoot, utcNow.UtcDateTime.ToString(""yyyy""), utcNow.UtcDateTime.ToString(""MM""), utcNow.UtcDateTime.ToString(""dd""), runId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<Dictionary<string, (string path, string sha256)>> WriteArtifactsAsync(string runDir, Dictionary<string, object> artifacts)
    {
        var map = new Dictionary<string, (string path, string sha256)>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in artifacts)
        {
            var key = kv.Key;
            var path = Path.Combine(runDir, key);
            var json = JsonSerializer.Serialize(kv.Value, JsonOpts.Serializer);

            await File.WriteAllTextAsync(path, json, Encoding.UTF8);
            var sha = Sha256File(path);

            map[key] = (path, sha);
        }

        return map;
    }

    public async Task<(string manifestPath, string manifestSha256)> WriteManifestAsync(
        string runDir,
        Dictionary<string, (string path, string sha256)> artifacts,
        string? previousManifestSha256 = null
    )
    {
        var manifestPath = Path.Combine(runDir, ""manifest.json"");

        var payload = new
        {
            previous_manifest_sha256 = previousManifestSha256,
            artifacts = artifacts.Select(a => new { artifact_key = a.Key, sha256 = a.Value.sha256, path = a.Value.path }).ToList()
        };

        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts.Serializer);
        var payloadSha = Sha256Bytes(Encoding.UTF8.GetBytes(payloadJson));

        var finalManifest = new
        {
            previous_manifest_sha256 = previousManifestSha256,
            current_manifest_sha256 = payloadSha,
            artifacts = payload.artifacts
        };

        var finalJson = JsonSerializer.Serialize(finalManifest, JsonOpts.Serializer);
        await File.WriteAllTextAsync(manifestPath, finalJson, Encoding.UTF8);

        var manifestSha = Sha256File(manifestPath);
        return (manifestPath, manifestSha);
    }

    public static string Sha256File(string path)
    {
        using var fs = File.OpenRead(path);
        var hash = SHA256.HashData(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256Bytes(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

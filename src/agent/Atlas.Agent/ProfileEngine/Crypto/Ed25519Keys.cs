using System.Text;
using System.Text.Json;
using Chaos.NaCl;

namespace Atlas.Agent.ProfileEngine.Crypto;

/// <summary>
/// v1 key material for Profile signing.
/// Storage: %LocalAppData%\Atlas\keys\profile_ed25519.json (Windows) / equivalent on other OS.
/// </summary>
public static class Ed25519Keys
{
    // Stored as base64:
    // - publicKey: 32 bytes
    // - privateKeySeed: 32 bytes (seed)  (canonical persisted form)
    //
    // At signing time we expand seed -> expandedPrivateKey (64 bytes) via Chaos.NaCl.
    public static (byte[] PublicKey, byte[] PrivateKeySeed) LoadOrCreate()
    {
        var path = GetKeyPath();
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var pk = Convert.FromBase64String(root.GetProperty("publicKeyB64").GetString()!);
            var seed = Convert.FromBase64String(root.GetProperty("privateKeyB64").GetString()!);
            return (pk, seed);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var seedNew = new byte[32];
        Random.Shared.NextBytes(seedNew);

        // Chaos.NaCl: seed -> pk + expanded sk
        var pkNew = Ed25519.PublicKeyFromSeed(seedNew);

        var obj = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = "atlas.keypair.v1",
            ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["publicKeyB64"] = Convert.ToBase64String(pkNew),
            ["privateKeyB64"] = Convert.ToBase64String(seedNew),
        };

        var bytes = CanonicalJson.SerializeDeterministic(obj);
        File.WriteAllBytes(path, bytes);

        return (pkNew, seedNew);
    }

    // Sign the 32-byte payload hash (sha256(manifest.json))
    public static byte[] Sign(byte[] privateKeySeed32, ReadOnlySpan<byte> payload32)
    {
        if (privateKeySeed32.Length != 32) throw new ArgumentException("privateKeySeed32 must be 32 bytes");
        if (payload32.Length != 32) throw new ArgumentException("payload32 must be 32 bytes");

        var expanded = Ed25519.ExpandedPrivateKeyFromSeed(privateKeySeed32);
        return Ed25519.Sign(payload32.ToArray(), expanded);
    }

    public static bool Verify(byte[] publicKey32, ReadOnlySpan<byte> payload32, byte[] signature64)
    {
        if (publicKey32.Length != 32) return false;
        if (payload32.Length != 32) return false;
        if (signature64.Length != 64) return false;

        return Ed25519.Verify(signature64, payload32.ToArray(), publicKey32);
    }

    private static string GetKeyPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Path.Combine(baseDir, "Atlas", "keys", "profile_ed25519.json");
    }
}
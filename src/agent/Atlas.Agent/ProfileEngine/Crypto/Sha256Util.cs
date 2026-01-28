using System.Security.Cryptography;

namespace Atlas.Agent.ProfileEngine.Crypto;

public static class Sha256Util
{
    public static string HashFileHex(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string HashBytesHex(ReadOnlySpan<byte> bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static byte[] HashBytes(ReadOnlySpan<byte> bytes)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(bytes.ToArray());
    }
}
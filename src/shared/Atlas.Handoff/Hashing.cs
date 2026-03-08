using System.Security.Cryptography;
using System.Text;

namespace Atlas.Handoff;

public static class Hashing
{
    public static byte[] Sha256(byte[] bytes)
    {
        bytes ??= Array.Empty<byte>();
        using var sha = SHA256.Create();
        return sha.ComputeHash(bytes);
    }

    public static string Sha256Hex(byte[] bytes) => CanonicalJson.ToHex(Sha256(bytes));

    public static string Sha256HexFromFile(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var h = sha.ComputeHash(fs);
        return CanonicalJson.ToHex(h);
    }

    public static byte[] Sha256FromFile(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return sha.ComputeHash(fs);
    }
}
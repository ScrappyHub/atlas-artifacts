using System.Security.Cryptography;
using System.Text;

namespace Atlas.Agent.ProfileEngine.Crypto;

public static class AesGcmEnvelope
{
    // ATENC1 envelope:
    // magic (6) = "ATENC1"
    // version (1) = 0x01
    // nonceLen (1) = 0x0c
    // nonce (12)
    // ciphertext+tag (rest)  (AesGcm tag = 16 bytes appended)
    public static void EncryptToFile(
        string outPath,
        byte[] key32,
        ReadOnlySpan<byte> plaintext,
        string aadText)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var aad = Encoding.UTF8.GetBytes(aadText);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var gcm = new AesGcm(key32, 16);
        gcm.Encrypt(nonce, plaintext.ToArray(), ciphertext, tag, aad);

        using var fs = File.Create(outPath);
        fs.Write(Encoding.ASCII.GetBytes("ATENC1"));
        fs.WriteByte(0x01);
        fs.WriteByte(0x0c);
        fs.Write(nonce, 0, nonce.Length);
        fs.Write(ciphertext, 0, ciphertext.Length);
        fs.Write(tag, 0, tag.Length);
    }

    public static byte[] DecryptFromFile(string inPath, byte[] key32, string aadText)
    {
        var bytes = File.ReadAllBytes(inPath);
        if (bytes.Length < 6 + 1 + 1 + 12 + 16) throw new InvalidDataException("envelope_too_small");

        var magic = Encoding.ASCII.GetString(bytes, 0, 6);
        if (magic != "ATENC1") throw new InvalidDataException("bad_magic");

        var ver = bytes[6];
        if (ver != 0x01) throw new InvalidDataException("bad_version");

        var nonceLen = bytes[7];
        if (nonceLen != 0x0c) throw new InvalidDataException("bad_nonce_len");

        var nonceOff = 8;
        var nonce = bytes.AsSpan(nonceOff, 12).ToArray();

        var aad = Encoding.UTF8.GetBytes(aadText);

        var ctOff = nonceOff + 12;
        var tagOff = bytes.Length - 16;

        var ciphertext = bytes.AsSpan(ctOff, tagOff - ctOff).ToArray();
        var tag = bytes.AsSpan(tagOff, 16).ToArray();

        var plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key32, 16);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return plaintext;
    }
}
using System;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Agent.Security;

public static class DeviceIdentity
{
    // Minimal, stable device fingerprint.
    // NOTE: This is not "hardware attestation" — just a deterministic token for local correlation.
    public static string GetDeviceId()
    {
        var machine = Environment.MachineName ?? "";
        var user = Environment.UserName ?? "";
        var os = Environment.OSVersion.VersionString ?? "";

        var input = $"{machine}|{user}|{os}";
        return Sha256Hex(input);
    }

    private static string Sha256Hex(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash = SHA256.HashData(bytes);

        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));

        return sb.ToString();
    }
}
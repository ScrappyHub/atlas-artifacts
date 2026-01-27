<<<<<<< HEAD
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Atlas.Agent.Security;

public static class DeviceIdentity
{
    public static string GetOrCreateDeviceId(string saltPath)
    {
        var salt = LoadOrCreateSalt(saltPath);
        var fp = GetFingerprintMaterial();
        var raw = $"{fp}|{Convert.ToHexString(salt)}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] LoadOrCreateSalt(string saltPath)
    {
        if (File.Exists(saltPath))
            return Convert.FromBase64String(File.ReadAllText(saltPath).Trim());

        var salt = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(saltPath, Convert.ToBase64String(salt));
        return salt;
    }

    private static string GetFingerprintMaterial()
    {
        // Privacy rule: do NOT store raw values in artifacts/logs. Only use derived hash.
        // Windows MachineGuid is relatively stable.
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var mg = key?.GetValue("MachineGuid")?.ToString() ?? "unknown";
            return $"win_machineguid:{mg}";
        }
        catch
        {
            return "win_machineguid:unavailable";
        }
    }
}
=======
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Atlas.Agent.Security;

public static class DeviceIdentity
{
    public static string GetOrCreateDeviceId(string saltPath)
    {
        var salt = LoadOrCreateSalt(saltPath);
        var fp = GetFingerprintMaterial();
        var raw = $""{fp}|{Convert.ToHexString(salt)}"";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] LoadOrCreateSalt(string saltPath)
    {
        if (File.Exists(saltPath))
            return Convert.FromBase64String(File.ReadAllText(saltPath).Trim());

        var salt = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(saltPath, Convert.ToBase64String(salt));
        return salt;
    }

    private static string GetFingerprintMaterial()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@""SOFTWARE\\Microsoft\\Cryptography"");
            var mg = key?.GetValue(""MachineGuid"")?.ToString() ?? ""unknown"";
            return $""win_machineguid:{mg}"";
        }
        catch
        {
            return ""win_machineguid:unavailable"";
        }
    }
}
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

using System.Security.Cryptography;
using System.Text;

static class Program
{
    // Canonical filenames
    private const string PrivateKeyName = "atlas-dev-private.pem";
    private const string PublicKeyName  = "atlas-dev-public.pem";

    static int Main(string[] args)
    {
        try
        {
            var repoRoot = Directory.GetCurrentDirectory();

            var devKeysDir = Path.Combine(repoRoot, "dev-keys");
            Directory.CreateDirectory(devKeysDir);

            var privPath = Path.Combine(devKeysDir, PrivateKeyName);
            var pubPath  = Path.Combine(devKeysDir, PublicKeyName);

            using var rsa = LoadOrCreateDevKeypair(privPath, pubPath);

            // Canonical license dir (Windows ProgramData; non-Windows fallback to ~/.local/share)
            var licDir = GetCanonicalLicenseDir();
            Directory.CreateDirectory(licDir);

            var jsonPath = Path.Combine(licDir, "atlas.license.json");
            var sigPath  = Path.Combine(licDir, "atlas.license.sig");

            // IMPORTANT: exact bytes are signed; keep compact, no newline.
            var licenseJson = "{\"license_id\":\"lic_dev_01\",\"customer\":\"Dev Customer\",\"tier\":\"enterprise\",\"device_limit\":250," +
                              "\"features\":[\"winget_scan\",\"artifacts\",\"rollback\",\"export_logs\",\"automation\"]," +
                              "\"issued_at\":\"2026-01-26T00:00:00Z\",\"expires_at\":null}";

            File.WriteAllBytes(jsonPath, Encoding.UTF8.GetBytes(licenseJson));

            var data = File.ReadAllBytes(jsonPath);
            var sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            File.WriteAllText(sigPath, Convert.ToBase64String(sig), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Verify immediately
            var ok = rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!ok) throw new Exception("Signature self-verify failed.");

            Console.WriteLine("DEV KEYS:");
            Console.WriteLine($"  {privPath}");
            Console.WriteLine($"  {pubPath}");
            Console.WriteLine("LICENSE:");
            Console.WriteLine($"  {jsonPath}");
            Console.WriteLine($"  {sigPath}");
            Console.WriteLine("Signature verified: OK");

            // Print public key PEM for easy copy/paste into LicenseVerifier.PublicKeyPem
            Console.WriteLine();
            Console.WriteLine("=== COPY THIS INTO LicenseVerifier.PublicKeyPem ===");
            Console.WriteLine(File.ReadAllText(pubPath));
            Console.WriteLine("=== END ===");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERR: {ex.Message}");
            return 1;
        }
    }

    private static RSA LoadOrCreateDevKeypair(string privPath, string pubPath)
    {
        // If private key exists, load it.
        if (File.Exists(privPath))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(privPath));
            if (!File.Exists(pubPath))
            {
                var pubPem = ExportPublicPem(rsa);
                File.WriteAllText(pubPath, pubPem, new UTF8Encoding(false));
            }
            return rsa;
        }

        // Else generate a new one.
        var created = RSA.Create(2048);

        var privPem = ExportPrivatePem(created);
        var pubPem  = ExportPublicPem(created);

        File.WriteAllText(privPath, privPem, new UTF8Encoding(false));
        File.WriteAllText(pubPath, pubPem, new UTF8Encoding(false));

        return created;
    }

    private static string ExportPrivatePem(RSA rsa)
    {
        // PKCS#8
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        return Pem("PRIVATE KEY", pkcs8);
    }

    private static string ExportPublicPem(RSA rsa)
    {
        // SubjectPublicKeyInfo
        var spki = rsa.ExportSubjectPublicKeyInfo();
        return Pem("PUBLIC KEY", spki);
    }

    private static string Pem(string label, byte[] der)
    {
        var b64 = Convert.ToBase64String(der);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        for (int i = 0; i < b64.Length; i += 64)
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }

    private static string GetCanonicalLicenseDir()
    {
        // Keep aligned with your docs: ProgramData/Atlas/Agent/licenses on Windows
        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "Atlas", "Agent", "licenses");
        }

        // mac/linux dev fallback
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "atlas", "agent", "licenses");
    }
}

using System;
using System.Security.Cryptography;

namespace Atlas.Agent.Licensing;

public static class LicenseVerifier
{
    // Replace with YOUR real public key later (PEM SubjectPublicKeyInfo).
    // Keep the agent shipping only the public key. Private key stays server-side.
    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
YOUR_PUBLIC_KEY_HERE
-----END PUBLIC KEY-----
""";

    public static bool Verify(byte[] licenseJsonBytes, string signatureBase64)
    {
        if (licenseJsonBytes is null || licenseJsonBytes.Length == 0)
            return false;

        if (string.IsNullOrWhiteSpace(signatureBase64))
            return false;

        byte[] sigBytes;
        try
        {
            sigBytes = Convert.FromBase64String(signatureBase64.Trim());
        }
        catch
        {
            return false;
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(PublicKeyPem);
        }
        catch
        {
            // Invalid PEM placeholder or malformed key.
            return false;
        }

        return rsa.VerifyData(
            licenseJsonBytes,
            sigBytes,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
    }
}

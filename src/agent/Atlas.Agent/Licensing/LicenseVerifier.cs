using System;
using System.IO;
using System.Security.Cryptography;

namespace Atlas.Agent.Licensing;

public static class LicenseVerifier
{
    public const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAs6TAVygHQD/iiyEbmM43
LC6rbKtkjX6lCfH4dPJnw+VoOKY9vChUW8KdvHKKbkz3N2lq1HFEmbSfZKwEIpbG
Z189U4MZqZk/wBXfqsvd45QBwFntkpgxiTxxEOgewGgkkyjd8v63eegxfqDhJ1Mm
PNEdPAL4BEVuZnVdYYMHIS3Ogb8ZJtp+KKuF2agGvplrhEWOjeahVf9zNZHxkGSr
RwDHaMzhUvW++a22JdSIIORYMO++vVVye5kwqZBDAOg31U5bjheTBuuwx4LubB8i
Qm+ms03GtN75e6SdTNTATiuOSpU11lKtt0vpYReQYGOhSlH1FPmU71uBcwQibzyo
nQIDAQAB
-----END PUBLIC KEY-----
""";

    public static bool Verify(string jsonPath, string sigPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PublicKeyPem))
                return false;

            if (!File.Exists(jsonPath) || !File.Exists(sigPath))
                return false;

            var data = File.ReadAllBytes(jsonPath);

            var sigB64 = File.ReadAllText(sigPath).Trim();
            if (string.IsNullOrWhiteSpace(sigB64))
                return false;

            byte[] sig;
            try { sig = Convert.FromBase64String(sigB64); }
            catch { return false; }

            using var rsa = RSA.Create();
            try { rsa.ImportFromPem(PublicKeyPem); }
            catch { return false; }

            return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
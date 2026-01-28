using System.Security.Cryptography;
using System.Text;

namespace Atlas.ActivationAuthority.Activation;

public static class GrantSigner
{
    // Deterministic HMAC signature, offline-safe, cross-platform.
    public static string Sign(string signingKey, string payload)
    {
        signingKey ??= "";
        payload ??= "";

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToBase64String(hash);
    }
}

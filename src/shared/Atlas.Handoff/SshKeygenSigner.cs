using System.Diagnostics;
using System.Text;

namespace Atlas.Handoff;

public static class SshKeygenSigner
{
    public static byte[] BuildSignBytesFromCommitHashHex(string commitHashHex)
    {
        // LOCKED: sign the ASCII line "sha256:<hex>\n"
        var line = "sha256:" + commitHashHex.ToLowerInvariant() + "\n";
        return Encoding.UTF8.GetBytes(line);
    }

    public static string ComputeKeyIdFromPublicKeyLine(string publicKeyLine)
    {
        // LOCKED: key_id = sha256(utf8(public_key_line_trimmed + "\n"))
        var norm = (publicKeyLine ?? "").Trim() + "\n";
        return Hashing.Sha256Hex(Encoding.UTF8.GetBytes(norm));
    }

    public static byte[] SignWithSshKeygen(string privateKeyPath, string principal, string sigNamespace, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPath)) throw new ArgumentException("privateKeyPath required");
        if (!File.Exists(privateKeyPath)) throw new FileNotFoundException("missing private key", privateKeyPath);

        var tmpDir = Path.Combine(Path.GetTempPath(), "atlas_handoff_sig_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmpDir);

        var dataPath = Path.Combine(tmpDir, "data.bin");
        var sigPath = dataPath + ".sig";

        File.WriteAllBytes(dataPath, data);

        // ssh-keygen -Y sign -f <key> -I <principal> -n <namespace> <file>
        var psi = new ProcessStartInfo
        {
            FileName = "ssh-keygen",
            Arguments = $"-Y sign -f \"{privateKeyPath}\" -I \"{principal}\" -n \"{sigNamespace}\" \"{dataPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("failed to start ssh-keygen");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException("ssh-keygen sign failed exit=" + p.ExitCode + "\n" + stdout + "\n" + stderr);

        if (!File.Exists(sigPath))
            throw new InvalidOperationException("ssh-keygen produced no signature file: " + sigPath);

        var sig = File.ReadAllBytes(sigPath);

        try { Directory.Delete(tmpDir, recursive: true); } catch { /* ignore */ }
        return sig;
    }
}
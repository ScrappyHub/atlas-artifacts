using System.Text;

namespace Atlas.Agent.ProfileEngine.Crypto;

public static class Sha256SumsWriter
{
    // Canonical:
    // UTF-8, LF, each line: "<hex><two spaces><path-with-/>", sorted by path ascending (ordinal)
    public static void Write(string path, IEnumerable<(string RelPath, string Sha256Hex)> entries)
    {
        var list = entries
            .Select(e => (RelPath: e.RelPath.Replace('\\', '/'), e.Sha256Hex))
            .OrderBy(e => e.RelPath, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        foreach (var e in list)
        {
            sb.Append(e.Sha256Hex);
            sb.Append("  ");
            sb.Append(e.RelPath);
            sb.Append('\n');
        }

        WriteUtf8NoBomLf(path, sb.ToString());
    }

    private static void WriteUtf8NoBomLf(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        content = content.Replace("\r\n", "\n");
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(path, content, utf8NoBom);
    }
}
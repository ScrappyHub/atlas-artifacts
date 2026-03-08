using System;
using System.IO;

namespace Atlas.Handoff;

public static class BlobStore
{
  public static string BlobRoot(string repoRoot)
    => Path.Combine(Path.GetFullPath(repoRoot), "data", "blobs");

  public static string PathForHex(string repoRoot, string hex)
  {
    if (string.IsNullOrWhiteSpace(hex)) throw new ArgumentException("hex required");
    return Path.Combine(BlobRoot(repoRoot), hex.ToLowerInvariant());
  }

  public static string PathForContentRef(string repoRoot, string contentRef)
  {
    var hex = ContentRef.RequireSha256Hex(contentRef);
    return PathForHex(repoRoot, hex);
  }

  public static string PutBlob(string repoRoot, byte[] bytes)
  {
    if (bytes is null) throw new ArgumentNullException(nameof(bytes));
    repoRoot = Path.GetFullPath(repoRoot);
    var hex = Hashing.Sha256Hex(bytes);
    var root = BlobRoot(repoRoot);
    Directory.CreateDirectory(root);
    var path = PathForHex(repoRoot, hex);
    if (!File.Exists(path))
    {
      File.WriteAllBytes(path, bytes);
    }
    return "sha256:" + hex;
  }
}

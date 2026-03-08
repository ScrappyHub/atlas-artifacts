using System;

namespace Atlas.Handoff;

public static class ContentRef
{
  public static string RequireSha256Hex(string contentRef)
  {
    if (string.IsNullOrWhiteSpace(contentRef)) throw new ArgumentException("contentRef required");
    contentRef = contentRef.Trim();

    const string pfx = "sha256:";
    if (!contentRef.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
      throw new ArgumentException("Unsupported contentRef (expected sha256:<hex>)");

    var hex = contentRef.Substring(pfx.Length).Trim();
    if (hex.Length != 64) throw new ArgumentException("sha256 hex must be 64 chars");

    for (int i = 0; i < hex.Length; i++)
    {
      char c = hex[i];
      bool ok =
        (c >= '0' && c <= '9') ||
        (c >= 'a' && c <= 'f') ||
        (c >= 'A' && c <= 'F');
      if (!ok) throw new ArgumentException("sha256 hex contains invalid chars");
    }

    return hex.ToLowerInvariant();
  }
}

using System.Text;
using System.Text.Json;

namespace Atlas.Handoff;

public sealed record AtlasInventorySnapshotV1(
 string schema,
 string device_id,
 string captured_utc,
 string hostname,
 string os_family,
 string os_version,
 string agent_version,
 string[] tags
);

public static class AtlasPayloads
{
 public static byte[] CanonicalBytes_InventorySnapshot(AtlasInventorySnapshotV1 obj)
 {
 var raw = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });
 return CanonicalJson.CanonicalizeToUtf8Bytes(raw);
 }

 public static string Utf8String(byte[] utf8) => Encoding.UTF8.GetString(utf8);
}

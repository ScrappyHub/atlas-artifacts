using System.Collections;
using System.Text;
using System.Text.Json;

namespace Atlas.Agent.ProfileEngine.Crypto;

public static class CanonicalJson
{
    // Deterministic JSON:
    // - sorted keys
    // - compact separators
    // - UTF-8 no BOM
    public static byte[] SerializeDeterministic(object value)
    {
        // Convert to a normalized tree using SortedDictionary recursively
        var normalized = Normalize(value);

        var opts = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        return JsonSerializer.SerializeToUtf8Bytes(normalized, opts);
    }

    private static object? Normalize(object? v)
    {
        if (v is null) return null;

        // Pass through primitives + strings
        if (v is string or bool) return v;
        if (v is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return v;

        // DateTimeOffset => ISO 8601
        if (v is DateTimeOffset dto) return dto.ToString("O");

        // IDictionary => SortedDictionary<string, object?>
        if (v is IDictionary dict)
        {
            var sd = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry de in dict)
            {
                var k = de.Key?.ToString() ?? "";
                sd[k] = Normalize(de.Value);
            }
            return sd;
        }

        // IEnumerable (but not string) => list
        if (v is IEnumerable seq and not string)
        {
            var list = new List<object?>();
            foreach (var item in seq) list.Add(Normalize(item));
            return list;
        }

        // POCO => serialize then parse then re-normalize via JsonElement
        var tmp = JsonSerializer.SerializeToUtf8Bytes(v, new JsonSerializerOptions { WriteIndented = false });
        using var doc = JsonDocument.Parse(tmp);
        return NormalizeJson(doc.RootElement);
    }

    private static object? NormalizeJson(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var sd = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var p in el.EnumerateObject())
                        sd[p.Name] = NormalizeJson(p.Value);
                    return sd;
                }
            case JsonValueKind.Array:
                {
                    var list = new List<object?>();
                    foreach (var item in el.EnumerateArray())
                        list.Add(NormalizeJson(item));
                    return list;
                }
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var i64)) return i64;
                if (el.TryGetDouble(out var d)) return d;
                return el.GetDecimal();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return null;
        }
    }

    public static void WriteFile(string path, object value)
    {
        var bytes = SerializeDeterministic(value);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public static void WriteHexFileWithNewline(string path, string hex)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, hex.ToLowerInvariant() + "\n", new UTF8Encoding(false));
    }
}
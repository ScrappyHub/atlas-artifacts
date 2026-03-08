using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Atlas.Handoff;

public static class CanonicalJson
{
    public static byte[] CanonicalizeToUtf8Bytes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var buffer = new ArrayBufferWriter<byte>(json.Length + 64);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            WriteElementCanonical(writer, doc.RootElement);
        }
        return NormalizeNewlinesToLf(buffer.WrittenSpan.ToArray());
    }

    public static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
        return sb.ToString();
    }

    public static byte[] Utf8NoBom(string s) => Encoding.UTF8.GetBytes(s);

    public static byte[] NormalizeNewlinesToLf(byte[] bytes)
    {
        // JSON itself should not contain CRLF, but we hard-normalize anyway.
        // Replace \r\n -> \n and bare \r -> \n.
        var outBytes = new List<byte>(bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b == 13) // \r
            {
                if (i + 1 < bytes.Length && bytes[i + 1] == 10) { i++; }
                outBytes.Add(10);
            }
            else outBytes.Add(b);
        }
        return outBytes.ToArray();
    }

    private static void WriteElementCanonical(Utf8JsonWriter w, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                // sort keys lexicographically
                var props = new List<JsonProperty>();
                foreach (var p in e.EnumerateObject()) props.Add(p);
                props.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var p in props)
                {
                    w.WritePropertyName(p.Name);
                    WriteElementCanonical(w, p.Value);
                }
                w.WriteEndObject();
                break;

            case JsonValueKind.Array:
                w.WriteStartArray();
                foreach (var it in e.EnumerateArray())
                    WriteElementCanonical(w, it);
                w.WriteEndArray();
                break;

            case JsonValueKind.String:
                w.WriteStringValue(e.GetString());
                break;

            case JsonValueKind.Number:
                // No floats in our schemas; still write the raw token to avoid formatting drift.
                w.WriteRawValue(e.GetRawText(), skipInputValidation: false);
                break;

            case JsonValueKind.True:
                w.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                w.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                w.WriteNullValue();
                break;

            default:
                throw new InvalidOperationException("Unsupported JSON kind: " + e.ValueKind);
        }
        w.Flush();
    }
}
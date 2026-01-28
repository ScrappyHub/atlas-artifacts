using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Atlas.Agent.Storage;

/// <summary>
/// Canonical artifact writer:
/// writes JSON artifacts under runs/{runId}/{name}.json
/// deterministic-ish: caller controls payload; writer uses UTF8 no BOM.
/// </summary>
public sealed class ArtifactWriter
{
    private readonly string _runsRoot;

    public ArtifactWriter(string runsRoot)
    {
        _runsRoot = runsRoot;
    }

    public string WriteJson(string runId, string name, object data)
    {
        if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("runId");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name");

        var dir = Path.Combine(_runsRoot, runId);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{Sanitize(name)}.json");

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
using System.IO;
using System.Text.Json;

namespace Atlas.Agent.Logging;

public sealed class ArtifactWriter
{
    private readonly string _runsRoot;

    public ArtifactWriter(string runsRoot)
    {
        _runsRoot = runsRoot;
        Directory.CreateDirectory(_runsRoot);
    }

    public string WriteJson(string runId, string name, object payload)
    {
        var runDir = Path.Combine(_runsRoot, runId);
        Directory.CreateDirectory(runDir);

        var path = Path.Combine(runDir, $"{name}.json");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }
}
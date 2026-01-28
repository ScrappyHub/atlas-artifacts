using System;
using System.IO;
using System.Text;
using System.Text.Json;
// NOTE: add project reference to Atlas.ActivationContracts when you wire this in.
using Atlas.ActivationContracts;

namespace Atlas.Agent.Activation;

// Canonical: server grant is source of truth; this is just a short-lived cache for offline grace.
public sealed class GrantStore
{
    private readonly string _path;

    public GrantStore(string path) => _path = path;

    public void Save(ActivationGrant grant)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(grant, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json, new UTF8Encoding(false));
    }

    public ActivationGrant? Load()
    {
        if (!File.Exists(_path)) return null;
        var json = File.ReadAllText(_path, new UTF8Encoding(false));
        return JsonSerializer.Deserialize<ActivationGrant>(json);
    }
}
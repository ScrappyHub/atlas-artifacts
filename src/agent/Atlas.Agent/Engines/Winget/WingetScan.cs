<<<<<<< HEAD
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Agent.Engines.Winget;

public static class WingetScan
{
    // Allowlisted commands (MVP)
    private const string EngineId = "windows.winget";

    public static async Task<(InventorySnapshot inventory, CandidatesSnapshot candidates, object summary)> ScanAsync(string deviceId, string runId)
    {
        // 1) inventory: prefer JSON output if supported; else parse table
        var inv = await GetInventoryAsync(deviceId);

        // 2) candidates: winget upgrade (available upgrades)
        var cands = await GetCandidatesAsync(runId, inv);

        var summary = new
        {
            engine_id = EngineId,
            inventory_count = inv.items.Count,
            candidate_count = cands.candidates.Count,
            captured_at = inv.captured_at
        };

        return (inv, cands, summary);
    }

    private static async Task<InventorySnapshot> GetInventoryAsync(string deviceId)
    {
        var capturedAt = DateTimeOffset.UtcNow.ToString("o");

        // Try JSON output (newer winget supports --output json)
        var resJson = await WingetRunner.RunAsync("list --accept-source-agreements --output json");
        if (resJson.ExitCode == 0 && LooksLikeJson(resJson.StdOut))
        {
            var items = ParseWingetListJson(resJson.StdOut);
            return new InventorySnapshot(deviceId, capturedAt, items);
        }

        // Fallback table parsing
        var res = resJson.ExitCode == 0 ? resJson : await WingetRunner.RunAsync("list --accept-source-agreements");
        var items2 = ParseWingetListTable(res.StdOut);
        return new InventorySnapshot(deviceId, capturedAt, items2);
    }

    private static async Task<CandidatesSnapshot> GetCandidatesAsync(string runId, InventorySnapshot inv)
    {
        var capturedAt = DateTimeOffset.UtcNow.ToString("o");

        var resJson = await WingetRunner.RunAsync("upgrade --accept-source-agreements --output json");
        if (resJson.ExitCode == 0 && LooksLikeJson(resJson.StdOut))
        {
            var candidates = ParseWingetUpgradeJson(resJson.StdOut, inv);
            return new CandidatesSnapshot(runId, capturedAt, candidates);
        }

        var res = resJson.ExitCode == 0 ? resJson : await WingetRunner.RunAsync("upgrade --accept-source-agreements");
        var candidates2 = ParseWingetUpgradeTable(res.StdOut, inv);
        return new CandidatesSnapshot(runId, capturedAt, candidates2);
    }

    private static bool LooksLikeJson(string s)
    {
        var t = s.TrimStart();
        return t.StartsWith("{") || t.StartsWith("[");
    }

    // ===== JSON parsing (best effort; winget JSON formats vary by version) =====

    private static List<InventoryItem> ParseWingetListJson(string json)
    {
        // We keep this resilient: try common fields; otherwise return empty.
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var rows = FindArray(root, "Data", "Sources", "Installed", "Packages") ?? FindFirstArray(root);
            if (rows is null) return new();

            var items = new List<InventoryItem>();
            foreach (var r in rows.Value.EnumerateArray())
            {
                var name = GetString(r, "Name", "PackageName") ?? "Unknown";
                var id = GetString(r, "Id", "PackageIdentifier") ?? name;
                var ver = GetString(r, "Version", "InstalledVersion") ?? "unknown";
                var source = GetString(r, "Source", "SourceName") ?? "winget";

                items.Add(new InventoryItem(
                    app_key: NormalizeKey(id),
                    display_name: name,
                    version: ver,
                    source: source,
                    vendor: null
                ));
            }
            return items;
        }
        catch
        {
            return new();
        }
    }

    private static List<CandidateItem> ParseWingetUpgradeJson(string json, InventorySnapshot inv)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var rows = FindArray(root, "Data", "Upgrades", "Packages") ?? FindFirstArray(root);
            if (rows is null) return new();

            var byKey = inv.items.ToDictionary(x => x.app_key, x => x, StringComparer.OrdinalIgnoreCase);

            var outList = new List<CandidateItem>();
            foreach (var r in rows.Value.EnumerateArray())
            {
                var name = GetString(r, "Name", "PackageName") ?? "Unknown";
                var pkgId = GetString(r, "Id", "PackageIdentifier") ?? name;

                var current = GetString(r, "Version", "InstalledVersion") ?? "unknown";
                var available = GetString(r, "AvailableVersion", "VersionAvailable", "UpgradeVersion") ?? "unknown";
                var source = GetString(r, "Source", "SourceName") ?? "winget";

                var appKey = NormalizeKey(pkgId);
                if (byKey.TryGetValue(appKey, out var invItem))
                    current = invItem.version;

                outList.Add(new CandidateItem(
                    app_key: appKey,
                    engine_id: "windows.winget",
                    package_id: pkgId,
                    current_version: current,
                    candidate_version: available,
                    requires_admin: true,          // conservative default; can refine per package later
                    source_type: "package_manager",
                    source_id: source,
                    evidence: null
                ));
            }
            return outList;
        }
        catch
        {
            return new();
        }
    }

    private static JsonElement? FindArray(JsonElement root, params string[] keys)
    {
        // Walk keys where each step may be object containing next
        JsonElement cur = root;
        foreach (var k in keys)
        {
            if (cur.ValueKind == JsonValueKind.Object && cur.TryGetProperty(k, out var next))
            {
                cur = next;
                continue;
            }
            return null;
        }
        return cur.ValueKind == JsonValueKind.Array ? cur : null;
    }

    private static JsonElement? FindFirstArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind != JsonValueKind.Object) return null;

        foreach (var p in root.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Array) return p.Value;
            if (p.Value.ValueKind == JsonValueKind.Object)
            {
                var inner = FindFirstArray(p.Value);
                if (inner is not null) return inner;
            }
        }
        return null;
    }

    private static string? GetString(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
            }
        }
        return null;
    }

    // ===== Table parsing fallback =====

    private static List<InventoryItem> ParseWingetListTable(string text)
    {
        // Typical winget list table: Name | Id | Version | Available | Source
        // We'll detect header + dashes then split by 2+ spaces.
        var lines = text.Split('\n').Select(x => x.TrimEnd('\r')).ToList();
        var start = FindTableStart(lines);
        if (start < 0) return new();

        var items = new List<InventoryItem>();
        for (int i = start; i < lines.Count; i++)
        {
            var ln = lines[i];
            if (string.IsNullOrWhiteSpace(ln)) continue;

            var cols = SplitCols(ln);
            if (cols.Count < 3) continue;

            // Heuristic: Name, Id, Version, ...
            var name = cols[0];
            var id = cols.Count > 1 ? cols[1] : name;
            var ver = cols.Count > 2 ? cols[2] : "unknown";
            var source = cols.Count > 4 ? cols[4] : (cols.Count > 3 ? cols[3] : "winget");

            items.Add(new InventoryItem(
                app_key: NormalizeKey(id),
                display_name: name,
                version: ver,
                source: source,
                vendor: null
            ));
        }
        return items;
    }

    private static List<CandidateItem> ParseWingetUpgradeTable(string text, InventorySnapshot inv)
    {
        var lines = text.Split('\n').Select(x => x.TrimEnd('\r')).ToList();
        var start = FindTableStart(lines);
        if (start < 0) return new();

        var byKey = inv.items.ToDictionary(x => x.app_key, x => x, StringComparer.OrdinalIgnoreCase);

        var outList = new List<CandidateItem>();
        for (int i = start; i < lines.Count; i++)
        {
            var ln = lines[i];
            if (string.IsNullOrWhiteSpace(ln)) continue;
            if (ln.StartsWith("No installed package", StringComparison.OrdinalIgnoreCase)) break;

            var cols = SplitCols(ln);
            if (cols.Count < 4) continue;

            var name = cols[0];
            var id = cols[1];
            var installed = cols[2];
            var available = cols[3];
            var source = cols.Count > 4 ? cols[4] : "winget";

            var appKey = NormalizeKey(id);
            if (byKey.TryGetValue(appKey, out var invItem))
                installed = invItem.version;

            outList.Add(new CandidateItem(
                app_key: appKey,
                engine_id: "windows.winget",
                package_id: id,
                current_version: installed,
                candidate_version: available,
                requires_admin: true,
                source_type: "package_manager",
                source_id: source,
                evidence: null
            ));
        }
        return outList;
    }

    private static int FindTableStart(List<string> lines)
    {
        // find dashed separator line and start after it
        for (int i = 0; i < lines.Count - 1; i++)
        {
            if (Regex.IsMatch(lines[i], @"^-{3,}\s*-{3,}"))
                return i + 1;
        }
        return -1;
    }

    private static List<string> SplitCols(string line)
    {
        // split on 2+ spaces to preserve names with single spaces
        return Regex.Split(line.Trim(), @"\s{2,}").Where(x => x.Length > 0).ToList();
    }

    private static string NormalizeKey(string s) => s.Trim().ToLowerInvariant();
}
=======
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Agent.Engines.Winget;

public static class WingetScan
{
    private const string EngineId = ""windows.winget"";

    public static async Task<(InventorySnapshot inventory, CandidatesSnapshot candidates, object summary)> ScanAsync(string deviceId, string runId)
    {
        var inv = await GetInventoryAsync(deviceId);
        var cands = await GetCandidatesAsync(runId, inv);

        var summary = new
        {
            engine_id = EngineId,
            inventory_count = inv.items.Count,
            candidate_count = cands.candidates.Count,
            captured_at = inv.captured_at
        };

        return (inv, cands, summary);
    }

    private static async Task<InventorySnapshot> GetInventoryAsync(string deviceId)
    {
        var capturedAt = DateTimeOffset.UtcNow.ToString(""o"");

        var resJson = await WingetRunner.RunAsync(""list --accept-source-agreements --output json"");
        if (resJson.ExitCode == 0 && LooksLikeJson(resJson.StdOut))
            return new InventorySnapshot(deviceId, capturedAt, ParseWingetListJson(resJson.StdOut));

        var res = resJson.ExitCode == 0 ? resJson : await WingetRunner.RunAsync(""list --accept-source-agreements"");
        return new InventorySnapshot(deviceId, capturedAt, ParseWingetListTable(res.StdOut));
    }

    private static async Task<CandidatesSnapshot> GetCandidatesAsync(string runId, InventorySnapshot inv)
    {
        var capturedAt = DateTimeOffset.UtcNow.ToString(""o"");

        var resJson = await WingetRunner.RunAsync(""upgrade --accept-source-agreements --output json"");
        if (resJson.ExitCode == 0 && LooksLikeJson(resJson.StdOut))
            return new CandidatesSnapshot(runId, capturedAt, ParseWingetUpgradeJson(resJson.StdOut, inv));

        var res = resJson.ExitCode == 0 ? resJson : await WingetRunner.RunAsync(""upgrade --accept-source-agreements"");
        return new CandidatesSnapshot(runId, capturedAt, ParseWingetUpgradeTable(res.StdOut, inv));
    }

    private static bool LooksLikeJson(string s)
    {
        var t = s.TrimStart();
        return t.StartsWith(""{""") || t.StartsWith(""["");
    }

    private static List<InventoryItem> ParseWingetListJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var rows = FindFirstArray(root);
            if (rows is null) return new();

            var items = new List<InventoryItem>();
            foreach (var r in rows.Value.EnumerateArray())
            {
                var name = GetString(r, ""Name"", ""PackageName"") ?? ""Unknown"";
                var id = GetString(r, ""Id"", ""PackageIdentifier"") ?? name;
                var ver = GetString(r, ""Version"", ""InstalledVersion"") ?? ""unknown"";
                var source = GetString(r, ""Source"", ""SourceName"") ?? ""winget"";

                items.Add(new InventoryItem(NormalizeKey(id), name, ver, source, null));
            }
            return items;
        }
        catch { return new(); }
    }

    private static List<CandidateItem> ParseWingetUpgradeJson(string json, InventorySnapshot inv)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var rows = FindFirstArray(root);
            if (rows is null) return new();

            var byKey = inv.items.ToDictionary(x => x.app_key, x => x, StringComparer.OrdinalIgnoreCase);

            var outList = new List<CandidateItem>();
            foreach (var r in rows.Value.EnumerateArray())
            {
                var name = GetString(r, ""Name"", ""PackageName"") ?? ""Unknown"";
                var pkgId = GetString(r, ""Id"", ""PackageIdentifier"") ?? name;

                var current = GetString(r, ""InstalledVersion"", ""Version"") ?? ""unknown"";
                var available = GetString(r, ""AvailableVersion"", ""VersionAvailable"", ""UpgradeVersion"") ?? ""unknown"";
                var source = GetString(r, ""Source"", ""SourceName"") ?? ""winget"";

                var appKey = NormalizeKey(pkgId);
                if (byKey.TryGetValue(appKey, out var invItem)) current = invItem.version;

                outList.Add(new CandidateItem(appKey, EngineId, pkgId, current, available, true, ""package_manager"", source, null));
            }
            return outList;
        }
        catch { return new(); }
    }

    private static List<InventoryItem> ParseWingetListTable(string text)
    {
        var lines = text.Split('\n').Select(x => x.TrimEnd('\r')).ToList();
        var start = FindTableStart(lines);
        if (start < 0) return new();

        var items = new List<InventoryItem>();
        for (int i = start; i < lines.Count; i++)
        {
            var ln = lines[i];
            if (string.IsNullOrWhiteSpace(ln)) continue;

            var cols = SplitCols(ln);
            if (cols.Count < 3) continue;

            var name = cols[0];
            var id = cols.Count > 1 ? cols[1] : name;
            var ver = cols.Count > 2 ? cols[2] : ""unknown"";
            var source = cols.Count > 4 ? cols[4] : (cols.Count > 3 ? cols[3] : ""winget"");

            items.Add(new InventoryItem(NormalizeKey(id), name, ver, source, null));
        }
        return items;
    }

    private static List<CandidateItem> ParseWingetUpgradeTable(string text, InventorySnapshot inv)
    {
        var lines = text.Split('\n').Select(x => x.TrimEnd('\r')).ToList();
        var start = FindTableStart(lines);
        if (start < 0) return new();

        var byKey = inv.items.ToDictionary(x => x.app_key, x => x, StringComparer.OrdinalIgnoreCase);

        var outList = new List<CandidateItem>();
        for (int i = start; i < lines.Count; i++)
        {
            var ln = lines[i];
            if (string.IsNullOrWhiteSpace(ln)) continue;
            if (ln.StartsWith(""No installed package"", StringComparison.OrdinalIgnoreCase)) break;

            var cols = SplitCols(ln);
            if (cols.Count < 4) continue;

            var id = cols[1];
            var installed = cols[2];
            var available = cols[3];
            var source = cols.Count > 4 ? cols[4] : ""winget"";

            var appKey = NormalizeKey(id);
            if (byKey.TryGetValue(appKey, out var invItem)) installed = invItem.version;

            outList.Add(new CandidateItem(appKey, EngineId, id, installed, available, true, ""package_manager"", source, null));
        }
        return outList;
    }

    private static int FindTableStart(List<string> lines)
    {
        for (int i = 0; i < lines.Count - 1; i++)
            if (Regex.IsMatch(lines[i], @"^-{3,}\s*-{3,}")) return i + 1;
        return -1;
    }

    private static List<string> SplitCols(string line) =>
        Regex.Split(line.Trim(), @"\s{2,}").Where(x => x.Length > 0).ToList();

    private static string NormalizeKey(string s) => s.Trim().ToLowerInvariant();

    private static string? GetString(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
            }
        }
        return null;
    }

    private static JsonElement? FindFirstArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind != JsonValueKind.Object) return null;

        foreach (var p in root.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Array) return p.Value;
            if (p.Value.ValueKind == JsonValueKind.Object)
            {
                var inner = FindFirstArray(p.Value);
                if (inner is not null) return inner;
            }
        }
        return null;
    }
}
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

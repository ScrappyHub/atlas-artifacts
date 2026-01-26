using Atlas.Agent.Engines.Winget;
using Atlas.Agent.Licensing;
using Atlas.Agent.Logging;
using Atlas.Agent.Storage;

namespace Atlas.Agent.IPC;

public sealed class CommandRouter
{
    private readonly AppPaths _paths;
    private readonly string _deviceId;
    private readonly LicenseService _license;
    private readonly ArtifactWriter _artifacts;
    private readonly RunStore _runs;

    public CommandRouter(AppPaths paths, string deviceId, LicenseService license)
    {
        _paths = paths;
        _deviceId = deviceId;
        _license = license;

        _artifacts = new ArtifactWriter(paths.ArtifactsDir);
        _runs = new RunStore(paths.DbPath);
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.user_id))
            return Fail(req.request_id, "auth_invalid", "user_id required");

        return req.command switch
        {
            "get_status" => Ok(req.request_id, new
            {
                agent = "Atlas.Agent",
                version = "0.1.0",
                device_id = _deviceId[..12] + "…"
            }),

            "get_license_status" => Ok(req.request_id, await _license.GetLicenseStatusAsync(_deviceId)),

            "request_scan" => await HandleScanAsync(req.request_id),

            "get_run_history" => Ok(req.request_id, new { runs = _runs.GetRunHistory(50) }),

            "get_run_details" => Ok(req.request_id, new
            {
                run = req.payload.HasValue && req.payload.Value.TryGetProperty("run_id", out var rid)
                    ? _runs.GetRunDetails(rid.GetString() ?? "")
                    : null
            }),

            _ => Fail(req.request_id, "unknown_command", $"Unknown command: {req.command}")
        };
    }

    private async Task<IpcResponse> HandleScanAsync(string requestId)
    {
        var runId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var runDir = _artifacts.CreateRunDir(runId, now);

        try
        {
            var (inventory, candidates, summary) = await WingetScan.ScanAsync(_deviceId, runId);

            var artifacts = new Dictionary<string, object>
            {
                ["inventory.json"] = inventory,
                ["candidates.json"] = candidates,
                ["summary.json"] = summary
            };

            var written = await _artifacts.WriteArtifactsAsync(runDir, artifacts);
            var (manifestPath, manifestSha) = await _artifacts.WriteManifestAsync(runDir, written);

            // Persist run + artifacts
            _runs.InsertRun(
                runId: runId,
                runType: "scan_run",
                createdAt: now.ToString("o"),
                outcome: "success",
                summary: summary
            );

            foreach (var kv in written)
                _runs.InsertArtifact(runId, kv.Key, kv.Value.path, kv.Value.sha256, now.ToString("o"));

            _runs.InsertArtifact(runId, "manifest.json", manifestPath, manifestSha, now.ToString("o"));

            return Ok(requestId, new
            {
                run_id = runId,
                outcome = "success",
                inventory_count = inventory.items.Count,
                candidate_count = candidates.candidates.Count
            });
        }
        catch (Exception ex)
        {
            var summary = new { error = ex.Message, stage = "scan_run" };

            _runs.InsertRun(
                runId: runId,
                runType: "scan_run",
                createdAt: now.ToString("o"),
                outcome: "failed",
                summary: summary
            );

            return Fail(requestId, "scan_failed", ex.Message);
        }
    }

    private static IpcResponse Ok(string requestId, object data) =>
        new(requestId, true, null, data);

    private static IpcResponse Fail(string requestId, string code, string message) =>
        new(requestId, false, new IpcError(code, message), null);
}

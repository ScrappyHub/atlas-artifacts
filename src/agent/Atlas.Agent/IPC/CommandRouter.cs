using System.Text.Json;
using Atlas.Agent.Engines.Winget;
using Atlas.Agent.Licensing;

namespace Atlas.Agent.IPC;

public sealed class CommandRouter
{
    // Canonical command names (string-based IPC contract)
    private static class Commands
    {
        public const string Ping = "ping";
        public const string GetStatus = "get_status";
        public const string GetLicenseStatus = "get_license_status";

        public const string ScanWinget = "scan_winget";

        // Reserved / next locks (implemented when engines are wired)
        public const string Rollback = "rollback";
        public const string ExportLogs = "export_logs";
        public const string Automation = "automation";
    }

    private readonly AppPaths _paths;
    private readonly string _deviceId;
    private readonly LicenseService _license;

    // Engines (can be null until wired; router stays canonical)
    private readonly WingetScan? _winget;

    public CommandRouter(AppPaths paths, string deviceId, LicenseService license, WingetScan? winget = null)
    {
        _paths = paths;
        _deviceId = deviceId;
        _license = license;
        _winget = winget;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest req, CancellationToken ct = default)
    {
        // Canonical request validation
        if (req is null) return Fail("unknown", "bad_request", "request required");
        if (string.IsNullOrWhiteSpace(req.request_id)) return Fail("unknown", "bad_request", "request_id required");
        if (string.IsNullOrWhiteSpace(req.command)) return Fail(req.request_id, "bad_request", "command required");
        if (string.IsNullOrWhiteSpace(req.user_id)) return Fail(req.request_id, "auth_invalid", "user_id required");

        try
        {
            // Fetch effective license status once per request (offline, local verify + tier caps)
            // This is your single source of truth for enforcement decisions.
            var status = await _license.GetLicenseStatusAsync(_deviceId);

            return req.command switch
            {
                Commands.Ping => Ok(req.request_id, new
                {
                    ok = true,
                    message = "pong"
                }),

                Commands.GetStatus => Ok(req.request_id, new
                {
                    agent = "Atlas.Agent",
                    // Keep version string here until you wire assembly versioning
                    version = "0.1.0",
                    device_id = _deviceId.Length >= 12 ? (_deviceId[..12] + "…") : _deviceId,
                    data_dir = _paths.DataDir,
                    artifacts_dir = _paths.ArtifactsDir,
                    licenses_dir = _paths.LicensesDir
                }),

                Commands.GetLicenseStatus => Ok(req.request_id, status),

                Commands.ScanWinget => await HandleScanWingetAsync(req, status.Features, ct),

                // Reserved: the canonical commands exist now, but the engines can be locked later.
                Commands.Rollback => Fail(req.request_id, "not_implemented", "rollback engine not wired yet"),
                Commands.ExportLogs => Fail(req.request_id, "not_implemented", "export_logs not wired yet"),
                Commands.Automation => Fail(req.request_id, "not_implemented", "automation not wired yet"),

                _ => Fail(req.request_id, "unknown_command", $"Unknown command: {req.command}")
            };
        }
        catch (LicenseDeniedException ex)
        {
            return Fail(req.request_id, "license_denied", ex.Message);
        }
        catch (JsonException ex)
        {
            return Fail(req.request_id, "bad_request", $"invalid JSON payload: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return Fail(req.request_id, "canceled", "request canceled");
        }
        catch (Exception ex)
        {
            return Fail(req.request_id, "internal_error", ex.Message);
        }
    }

    private async Task<IpcResponse> HandleScanWingetAsync(IpcRequest req, Feature licensedFeatures, CancellationToken ct)
    {
        // Canonical enforcement
        LicenseGates.RequireFeature(licensedFeatures, Feature.WingetScan, "WingetScan");

        if (_winget is null)
            return Fail(req.request_id, "not_implemented", "winget engine not wired");

        var pkgs = await _winget.ScanAsync(ct);

        return Ok(req.request_id, new
        {
            packages = pkgs,
            count = pkgs.Count
        });
    }

    private static IpcResponse Ok(string requestId, object? data) =>
        new(requestId, true, null, data);

    private static IpcResponse Fail(string requestId, string code, string message) =>
        new(requestId, false, new IpcError(code, message), null);
}

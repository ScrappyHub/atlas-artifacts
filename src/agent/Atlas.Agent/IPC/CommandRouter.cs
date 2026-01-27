using System;
using System.Threading.Tasks;
using Atlas.Agent.Licensing;

namespace Atlas.Agent.IPC;

public sealed class CommandRouter
{
    private readonly AppPaths _paths;
    private readonly string _deviceId;
    private readonly LicenseService _license;

    public CommandRouter(AppPaths paths, string deviceId, LicenseService license)
    {
        _paths = paths;
        _deviceId = deviceId;
        _license = license;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest req)
    {
        // Phase 1 auth: user_id required (future: OS user → role mapping).
        if (string.IsNullOrWhiteSpace(req.user_id))
            return Fail(req.request_id, "auth_invalid", "user_id required");

        try
        {
            // Always compute status once per request (canonical).
            // Assumes your LicenseService returns an object with:
            // - bool Licensed (optional)
            // - Feature Features
            // - int DeviceLimit (optional)
            // - DateTimeOffset? ExpiresAt (optional)
            var status = await _license.GetLicenseStatusAsync(_deviceId);

            return req.command switch
            {
                IpcCommands.GetStatus => Ok(req.request_id, new
                {
                    agent = "Atlas.Agent",
                    version = "0.1.0",
                    device_id = _deviceId.Length > 12 ? _deviceId[..12] + "…" : _deviceId
                }),

                IpcCommands.GetLicenseStatus => Ok(req.request_id, status),

                // Baseline scan request (gated by WingetScan feature)
                IpcCommands.RequestScan => HandleRequestScan(req, status),

                // Rollback (gated)
                IpcCommands.RollbackCreateSnapshot => HandleRollbackCreateSnapshot(req, status),
                IpcCommands.RollbackRestore => HandleRollbackRestore(req, status),

                // Export logs (gated)
                IpcCommands.ExportLogs => HandleExportLogs(req, status),

                // Automation (gated)
                IpcCommands.AutomationRun => HandleAutomation(req, status),

                _ => Fail(req.request_id, "unknown_command", $"Unknown command: {req.command}")
            };
        }
        catch (LicenseDeniedException ex)
        {
            return Fail(req.request_id, "license_denied", ex.Message);
        }
        catch (Exception ex)
        {
            return Fail(req.request_id, "internal_error", ex.Message);
        }
    }

    private IpcResponse HandleRequestScan(IpcRequest req, dynamic status)
    {
        // Gate: WingetScan
        Feature features = status.Features;
        LicenseGates.RequireFeature(features, Feature.WingetScan, "WingetScan");

        // If your Winget engine is wired later, this becomes real.
        return Ok(req.request_id, new
        {
            accepted = true,
            note = "Scan stub accepted (engine wiring next)."
        });
    }

    private IpcResponse HandleRollbackCreateSnapshot(IpcRequest req, dynamic status)
    {
        Feature features = status.Features;
        LicenseGates.RequireFeature(features, Feature.Rollback, "Rollback");

        return Fail(req.request_id, "not_implemented", "Rollback snapshot not wired yet.");
    }

    private IpcResponse HandleRollbackRestore(IpcRequest req, dynamic status)
    {
        Feature features = status.Features;
        LicenseGates.RequireFeature(features, Feature.Rollback, "Rollback");

        return Fail(req.request_id, "not_implemented", "Rollback restore not wired yet.");
    }

    private IpcResponse HandleExportLogs(IpcRequest req, dynamic status)
    {
        Feature features = status.Features;
        LicenseGates.RequireFeature(features, Feature.ExportLogs, "ExportLogs");

        return Fail(req.request_id, "not_implemented", "ExportLogs not wired yet.");
    }

    private IpcResponse HandleAutomation(IpcRequest req, dynamic status)
    {
        Feature features = status.Features;
        LicenseGates.RequireFeature(features, Feature.Automation, "Automation");

        return Fail(req.request_id, "not_implemented", "Automation not wired yet.");
    }

    private static IpcResponse Ok(string requestId, object data) =>
        new(requestId, true, null, data);

    private static IpcResponse Fail(string requestId, string code, string message) =>
        new(requestId, false, new IpcError(code, message), null);
}

/// <summary>
/// Canonical command names (string-based IPC).
/// Keep stable across OS + versions.
/// </summary>
public static class IpcCommands
{
    public const string GetStatus = "get_status";
    public const string GetLicenseStatus = "get_license_status";
    public const string RequestScan = "request_scan";

    public const string RollbackCreateSnapshot = "rollback_create_snapshot";
    public const string RollbackRestore = "rollback_restore";

    public const string ExportLogs = "export_logs";
    public const string AutomationRun = "automation_run";
}

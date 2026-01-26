using System.Text.Json;
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
        // Phase 1: minimal auth. Future: map OS user → role.
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

            "request_scan" => Ok(req.request_id, new
            {
                accepted = true,
                note = "Scan not implemented yet (next lock step: winget engine + run artifacts)."
            }),

            _ => Fail(req.request_id, "unknown_command", $"Unknown command: {req.command}")
        };
    }

    private static IpcResponse Ok(string requestId, object data) =>
        new(requestId, true, null, data);

    private static IpcResponse Fail(string requestId, string code, string message) =>
        new(requestId, false, new IpcError(code, message), null);
}

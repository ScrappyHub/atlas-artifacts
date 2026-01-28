using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Agent.Engines.Winget;
using Atlas.Agent.Licensing;

namespace Atlas.Agent.IPC;

public sealed class CommandRouter
{
    private readonly WingetScan _winget;
    private readonly LicenseService _license;
    private readonly string _deviceId;

    public CommandRouter(WingetScan winget, LicenseService license, string deviceId)
    {
        _winget = winget;
        _license = license;
        _deviceId = deviceId;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest req, CancellationToken ct)
    {
        try
        {
            switch (req.Command)
            {
                case IpcCommand.Ping:
                    return IpcResponse.Success(req.RequestId, "pong");

                case IpcCommand.GetLicenseStatus:
                    {
                        var status = await _license.GetLicenseStatusAsync(_deviceId);
                        return IpcResponse.Success(req.RequestId, "ok", status);
                    }

                case IpcCommand.ScanWinget:
                    {
                        // Winget scan must be licensed as a FEATURE (even if supported/not supported by OS).
                        var status = await _license.GetLicenseStatusAsync(_deviceId);
                        LicenseGates.RequireLicensed(status);
                        LicenseGates.RequireFeature(status.Features, Feature.WingetScan, "WingetScan");

                        var pkgs = await _winget.ScanAsync(ct);
                        return IpcResponse.Success(req.RequestId, "ok", pkgs);
                    }

                default:
                    return IpcResponse.Fail(req.RequestId, $"unknown_command: {req.Command}");
            }
        }
        catch (LicenseDeniedException ex)
        {
            return IpcResponse.Fail(req.RequestId, ex.Message);
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(req.RequestId, $"internal_error: {ex.Message}");
        }
    }
}
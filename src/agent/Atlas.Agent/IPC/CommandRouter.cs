using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Agent.Engines.Winget;
using Atlas.Agent.Licensing;

namespace Atlas.Agent.IPC;

public sealed class CommandRouter
{
    private readonly WingetScan _winget;
    private readonly LicenseService _licenseService;
    private readonly string _deviceId;

    public CommandRouter(WingetScan winget, LicenseService licenseService, string deviceId)
    {
        _winget = winget;
        _licenseService = licenseService;
        _deviceId = deviceId;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest req, CancellationToken ct)
    {
        try
        {
            // License status is the canonical enforcement input for every request.
            var status = await _licenseService.GetLicenseStatusAsync(_deviceId);

            switch (req.Command)
            {
                case IpcCommand.Ping:
                    return IpcResponse.Ok("pong");

                case IpcCommand.ScanWinget:
                {
                    LicenseGates.RequireFeature(status.Features, Feature.WingetScan, "WingetScan");

                    var pkgs = await _winget.ScanAsync(ct);
                    return IpcResponse.Ok("scan_complete", payload: pkgs);
                }

                case IpcCommand.Rollback:
                {
                    LicenseGates.RequireFeature(status.Features, Feature.Rollback, "Rollback");
                    // TODO: call RollbackEngine here once wired (keep router contract stable)
                    return IpcResponse.Fail("Rollback engine not wired yet.");
                }

                case IpcCommand.ExportLogs:
                {
                    LicenseGates.RequireFeature(status.Features, Feature.ExportLogs, "ExportLogs");
                    // TODO: implement export routine
                    return IpcResponse.Fail("ExportLogs not wired yet.");
                }

                case IpcCommand.Automation:
                {
                    LicenseGates.RequireFeature(status.Features, Feature.Automation, "Automation");
                    // TODO: implement automation routine
                    return IpcResponse.Fail("Automation not wired yet.");
                }

                default:
                    return IpcResponse.Fail($"Unknown command: {req.Command}");
            }
        }
        catch (LicenseDeniedException ex)
        {
            return IpcResponse.Fail(ex.Message);
        }
        catch (OperationCanceledException)
        {
            return IpcResponse.Fail("Cancelled.");
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail($"Internal error: {ex.Message}");
        }
    }
}

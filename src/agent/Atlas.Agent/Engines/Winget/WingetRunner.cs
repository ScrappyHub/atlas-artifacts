using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Agent.Engines.Winget;

public sealed class WingetRunner
{
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string args, CancellationToken ct)
    {
        if (!IsSupported)
            return (ExitCode: 126, StdOut: "", StdErr: "winget is Windows-only");

        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start winget.");
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        await p.WaitForExitAsync(ct);
        return (p.ExitCode, await stdoutTask, await stderrTask);
    }
}

using System.Diagnostics;
using System.Text;

namespace Atlas.Agent.Engines.Winget;

public sealed record ProcResult(int ExitCode, string StdOut, string StdErr);

public static class WingetRunner
{
    public static async Task<ProcResult> RunAsync(string args, int timeoutMs = 120_000)
    {
        // Hard rule: do not accept arbitrary user-provided args in Phase 1.
        // All callers must pass a constant allowlisted command string.
        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        p.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        if (!p.Start())
            throw new InvalidOperationException("Failed to start winget");

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"winget timed out: winget {args}");
        }

        return new ProcResult(p.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Agent.Engines.Winget;

public sealed class WingetScan
{
    private readonly WingetRunner _runner;

    public WingetScan(WingetRunner runner) => _runner = runner;

    public async Task<IReadOnlyList<WingetPackage>> ScanAsync(CancellationToken ct)
    {
        var (code, _, stderr) = await _runner.RunAsync("list", ct);

        var source =
            !_runner.IsSupported ? "winget (not_supported)" :
            string.IsNullOrWhiteSpace(stderr) ? "winget" :
            $"winget (stderr={stderr.Length})";

        return new List<WingetPackage>
        {
            new WingetPackage(
                Id: "winget:list",
                Name: $"exit={code}",
                Version: "n/a",
                Source: source)
        };
    }
}

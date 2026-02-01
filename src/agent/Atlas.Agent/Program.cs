using System.Text;
using System.Text.Json;
using Atlas.ActivationContracts;

var tenantId = Environment.GetEnvironmentVariable("ATLAS_TENANT_ID") ?? "t_dev";
var deviceId = Environment.GetEnvironmentVariable("ATLAS_DEVICE_ID") ?? Environment.MachineName;
var baseUrl = Environment.GetEnvironmentVariable("ATLAS_AUTHORITY_URL") ?? "http://127.0.0.1:5000";

var pollSeconds = int.TryParse(Environment.GetEnvironmentVariable("ATLAS_POLL_SECONDS"), out var ps) ? ps : 3;
var cacheDir = Environment.GetEnvironmentVariable("ATLAS_CACHE_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Atlas", "cache");

Directory.CreateDirectory(cacheDir);

Console.WriteLine($"Atlas.Agent starting");
Console.WriteLine($"Authority={baseUrl} TenantId={tenantId} DeviceId={deviceId} Cache={cacheDir}");
/* ATLAS_A1_HELLO_BEGIN */
static string DetectOs()
{
    if (OperatingSystem.IsWindows()) return "windows";
    if (OperatingSystem.IsMacOS()) return "macos";
    if (OperatingSystem.IsLinux()) return "linux";
    return "unknown";
}

static string DetectArch()
{
    return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
}

static string DetectAgentVersion()
{
    return typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}

static string[] DetectCapabilities()
{
    return new[] { "inventory" };
}

static async Task<bool> PostHelloAsync(HttpClient http, string baseUrl, string tenantId, string deviceId, CancellationToken ct)
{
    try
    {
        var url = $"{baseUrl}/v1/agents/hello";
        var req = new Atlas.ActivationContracts.AgentSpine.AgentHelloRequest(
            tenantId: tenantId,
            deviceId: deviceId,
            os: DetectOs(),
            arch: DetectArch(),
            agentVersion: DetectAgentVersion(),
            capabilities: DetectCapabilities()
        );

        var json = System.Text.Json.JsonSerializer.Serialize(req);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        Console.WriteLine($"HELLO status={(int)resp.StatusCode} body={body}");
        return resp.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
        Console.WriteLine("HELLO error: " + ex.Message);
        return false;
    }
}
/* ATLAS_A1_HELLO_END */

using var cts = new CancellationTokenSource();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
/* ATLAS_A1_HELLO_CALL */
await PostHelloAsync(http, baseUrl, tenantId, deviceId, cts.Token);
/* ATLAS_A1_HELLO_CALL_END */

while (true)
{
    try
    {
        var pollUrl = $"{baseUrl}/v1/jobs/poll?tenantId={Uri.EscapeDataString(tenantId)}&deviceId={Uri.EscapeDataString(deviceId)}&max=3";
        var json = await http.GetStringAsync(pollUrl);
        var resp = JsonSerializer.Deserialize<PollJobsResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (resp?.Ok == true && resp.Jobs.Length > 0)
        {
            foreach (var job in resp.Jobs)
            {
                Console.WriteLine($"Job {job.JobId} {job.Type} queued");

                // Execute job (best-effort). ResultJson is emitted to stdout for now.
                var result = await ExecuteJobAsync(job, http, cacheDir);

                Console.WriteLine($"Job {job.JobId} result: {result}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Poll error: " + ex.Message);
    }

    await Task.Delay(TimeSpan.FromSeconds(pollSeconds));
}

static async Task<string> ExecuteJobAsync(AtlasJob job, HttpClient http, string cacheDir)
{
    try
    {
        switch (job.Type)
        {
            case AtlasJobType.Inventory:
                return Inventory();

            case AtlasJobType.Download:
            case (AtlasJobType)100: // ProfileExport
                {
                    var payload = Atlas.Agent.ProfileEngine.ProfileJson.Deserialize<Atlas.ActivationContracts.Profiles.ProfileExportPayload>(job.PayloadJson ?? "{}");
                    var result = Atlas.Agent.ProfileEngine.ProfileExportEngine.Execute(payload);
                    return JsonSerializer.Serialize(result);
                }

            case (AtlasJobType)101: // ProfileRestore
                {
                    var payload = Atlas.Agent.ProfileEngine.ProfileJson.Deserialize<Atlas.ActivationContracts.Profiles.ProfileRestorePayload>(job.PayloadJson ?? "{}");
                    var result = Atlas.Agent.ProfileEngine.ProfileRestoreEngine.Restore(payload);
                    return JsonSerializer.Serialize(result);
                }

            case (AtlasJobType)102: // ProfileVerify
                {
                    var payload = Atlas.Agent.ProfileEngine.ProfileJson.Deserialize<Atlas.ActivationContracts.Profiles.ProfileRestorePayload>(job.PayloadJson ?? "{}");
                    var result = Atlas.Agent.ProfileEngine.ProfileVerifyEngine.Verify(payload);
                    return JsonSerializer.Serialize(result);
                }

                return await DownloadAsync(job.PayloadJson, http, cacheDir);

            case AtlasJobType.Install:
                return Install(job.PayloadJson);

            case AtlasJobType.Stop:
                return StopProcesses(job.PayloadJson);

            case AtlasJobType.Restore:
                return RestoreWindows(job.PayloadJson);


            default:
                return JsonSerializer.Serialize(new { ok = false, code = "unknown_job_type" });
        }
    }
    catch (Exception ex)
    {
        return JsonSerializer.Serialize(new { ok = false, code = "exception", error = ex.Message });
    }
}

static string Inventory()
{
    var apps = new List<InventoryApp>();
    var errors = new List<InventoryError>();

    string platform =
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsMacOS() ? "macos" :
        OperatingSystem.IsLinux() ? "linux" :
        "unknown";

    try
    {
        if (OperatingSystem.IsWindows())
            InventoryWindows(apps, errors);
        else if (OperatingSystem.IsMacOS())
            InventoryMac(apps, errors);
        else if (OperatingSystem.IsLinux())
            InventoryLinux(apps, errors);
        else
            errors.Add(new InventoryError("platform", -1, "Unsupported OS"));
    }
    catch (Exception ex)
    {
        errors.Add(new InventoryError("inventory", -1, ex.Message));
    }

    var result = new InventoryResult(
        ok: true,
        platform: platform,
        schemaVersion: 1,
        count: apps.Count,
        apps: apps,
        errors: errors
    );

    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static void InventoryWindows(List<InventoryApp> apps, List<InventoryError> errors)
{
    try
    {
        void ReadKey(Microsoft.Win32.RegistryKey? root, string path, string scope)
        {
            using var k = root?.OpenSubKey(path);
            if (k == null) return;

            foreach (var subName in k.GetSubKeyNames())
            {
                using var sub = k.OpenSubKey(subName);
                if (sub == null) continue;

                var displayName = sub.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(displayName)) continue;

                var displayVersion = sub.GetValue("DisplayVersion")?.ToString();
                var publisher = sub.GetValue("Publisher")?.ToString();
                var installLocation = sub.GetValue("InstallLocation")?.ToString();
                var uninstallString = sub.GetValue("UninstallString")?.ToString();

                apps.Add(new InventoryApp(
                    id: $"winreg:{path}\\{subName}",
                    name: displayName!,
                    version: displayVersion,
                    publisher: publisher,
                    installLocation: installLocation,
                    uninstall: uninstallString,
                    source: "registry",
                    scope: scope,
                    arch: null,
                    meta: new Dictionary<string, string?>()
                ));
            }
        }

        ReadKey(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "machine");
        ReadKey(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "machine");
    }
    catch (Exception ex)
    {
        errors.Add(new InventoryError("registry", -1, ex.Message));
    }
}

static void InventoryMac(List<InventoryApp> apps, List<InventoryError> errors)
{
    var (code, stdout, stderr) = RunProcess("system_profiler", "SPApplicationsDataType -json");
    if (code != 0 || string.IsNullOrWhiteSpace(stdout))
    {
        errors.Add(new InventoryError("system_profiler", code, string.IsNullOrWhiteSpace(stderr) ? "failed" : stderr));
        return;
    }

    try
    {
        using var doc = JsonDocument.Parse(stdout);
        if (!doc.RootElement.TryGetProperty("SPApplicationsDataType", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new InventoryError("system_profiler", 0, "missing SPApplicationsDataType array"));
            return;
        }

        foreach (var a in arr.EnumerateArray())
        {
            var name = a.TryGetProperty("_name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var version = a.TryGetProperty("version", out var v) ? v.GetString() : null;
            var path = a.TryGetProperty("path", out var p) ? p.GetString() : null;

            var id = !string.IsNullOrWhiteSpace(path) ? $"macapp:{path}" : $"macapp:{name}";

            apps.Add(new InventoryApp(
                id: id,
                name: name!,
                version: version,
                publisher: null,
                installLocation: path,
                uninstall: null,
                source: "system_profiler",
                scope: "machine",
                arch: null,
                meta: new Dictionary<string, string?>()
            ));
        }
    }
    catch (Exception ex)
    {
        errors.Add(new InventoryError("system_profiler.parse", -1, ex.Message));
    }
}

static void InventoryLinux(List<InventoryApp> apps, List<InventoryError> errors)
{
    var successes = 0;

    if (TryAppendLinuxPackages(apps, "dpkg-query", "-W -f='${Package}\t${Version}\n'", "dpkg", out var e1)) successes++;
    else if (e1 != null) errors.Add(e1);

    if (TryAppendLinuxPackages(apps, "rpm", "-qa --qf '%{NAME}\t%{VERSION}-%{RELEASE}\n'", "rpm", out var e2)) successes++;
    else if (e2 != null) errors.Add(e2);

    if (TryAppendLinuxPackages(apps, "pacman", "-Q", "pacman", out var e3)) successes++;
    else if (e3 != null) errors.Add(e3);

    if (TryAppendLinuxPackages(apps, "apk", "info -v", "apk", out var e4)) successes++;
    else if (e4 != null) errors.Add(e4);

    if (successes == 0)
        errors.Add(new InventoryError("linux.inventory", 0, "No supported package manager output available (dpkg/rpm/pacman/apk)."));
}

static bool TryAppendLinuxPackages(
    List<InventoryApp> apps,
    string fileName,
    string args,
    string source,
    out InventoryError? error)
{
    error = null;

    var (code, stdout, stderr) = RunProcess(fileName, args);

    if (code != 0 || string.IsNullOrWhiteSpace(stdout))
    {
        var msg = string.IsNullOrWhiteSpace(stderr) ? "failed" : stderr;
        error = new InventoryError(source, code, msg);
        return false;
    }

    var lines = stdout.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
    foreach (var line in lines)
    {
        var partsTab = line.Split('\t', 2);
        string name;
        string? version;

        if (partsTab.Length >= 2)
        {
            name = partsTab[0].Trim().Trim('\'');
            version = partsTab[1].Trim().Trim('\'');
        }
        else
        {
            var partsSpace = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            name = partsSpace.Length > 0 ? partsSpace[0].Trim() : "";
            version = partsSpace.Length > 1 ? partsSpace[1].Trim() : null;
        }

        if (string.IsNullOrWhiteSpace(name)) continue;

        apps.Add(new InventoryApp(
            id: $"linuxpkg:{name}",
            name: name,
            version: version,
            publisher: null,
            installLocation: null,
            uninstall: null,
            source: source,
            scope: "machine",
            arch: null,
            meta: new Dictionary<string, string?>()
        ));
    }

    return true;
}

static (int ExitCode, string StdOut, string StdErr) RunProcess(string fileName, string args)
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = System.Diagnostics.Process.Start(psi);
        if (p == null) return (-1, "", "Process start returned null.");

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();

        // Wait briefly; if it times out, treat as error but don't crash agent.
        if (!p.WaitForExit(10_000))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return (-1, stdout, "Timed out");
        }

        return (p.ExitCode, stdout, stderr);
    }
    catch (Exception ex)
    {
        // Common on Linux when binary not found: Win32Exception
        return (-1, "", ex.Message);
    }
}

static async Task<string> DownloadAsync(string payloadJson, HttpClient http, string cacheDir)
{
    // payload: { "url": "...", "fileName": "...optional..." }
    using var doc = JsonDocument.Parse(payloadJson);
    var url = doc.RootElement.GetProperty("url").GetString() ?? throw new Exception("payload.url missing");

    var fileName = doc.RootElement.TryGetProperty("fileName", out var fn)
        ? (fn.GetString() ?? "")
        : "";

    if (string.IsNullOrWhiteSpace(fileName))
    {
        fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "download.bin";
    }

    var dst = Path.Combine(cacheDir, fileName);

    using var resp = await http.GetAsync(url);
    resp.EnsureSuccessStatusCode();

    await using var fs = File.Create(dst);
    await resp.Content.CopyToAsync(fs);

    return JsonSerializer.Serialize(new { ok = true, path = dst });
}

static string Install(string payloadJson)
{
    // payload: { "path": "C:\\...\\installer.exe|msi", "args": "...optional..." }
    using var doc = JsonDocument.Parse(payloadJson);
    var path = doc.RootElement.GetProperty("path").GetString() ?? throw new Exception("payload.path missing");
    var args = doc.RootElement.TryGetProperty("args", out var a) ? (a.GetString() ?? "") : "";

    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = path,
        Arguments = args,
        UseShellExecute = true, // allows elevation prompt if needed
        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
    };

    var p = System.Diagnostics.Process.Start(psi) ?? throw new Exception("failed_to_start");
    p.WaitForExit();

    return JsonSerializer.Serialize(new { ok = p.ExitCode == 0, exitCode = p.ExitCode });
}

static string StopProcesses(string payloadJson)
{
    // payload: { "names": ["Discord","Creative Cloud","ChatGPT"] }
    using var doc = JsonDocument.Parse(payloadJson);
    if (!doc.RootElement.TryGetProperty("names", out var arr) || arr.ValueKind != JsonValueKind.Array)
        throw new Exception("payload.names missing");

    var killed = new List<string>();

    foreach (var e in arr.EnumerateArray())
    {
        var name = e.GetString();
        if (string.IsNullOrWhiteSpace(name)) continue;

        foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
        {
            try { p.Kill(true); killed.Add($"{name}:{p.Id}"); } catch { /* ignore */ }
        }
    }

    return JsonSerializer.Serialize(new { ok = true, killed });
}

static string RestoreWindows(string payloadJson)
{
    // REAL restore plane (Windows System Restore):
    // payload: { "mode": "checkpoint" } OR { "mode":"restore", "restorePointDescription":"..." }
    // Note: System Restore must be enabled; this will error clearly if not.
    using var doc = JsonDocument.Parse(payloadJson);
    var mode = doc.RootElement.TryGetProperty("mode", out var m) ? (m.GetString() ?? "") : "";

    if (mode.Equals("checkpoint", StringComparison.OrdinalIgnoreCase))
    {
        var desc = doc.RootElement.TryGetProperty("description", out var d) ? (d.GetString() ?? "Atlas checkpoint") : "Atlas checkpoint";
        return RunPowerShell($"Checkpoint-Computer -Description \"{desc}\" -RestorePointType \"MODIFY_SETTINGS\"");
    }

    if (mode.Equals("restore", StringComparison.OrdinalIgnoreCase))
    {
        // This requires a restore point and reboot; Restore-Computer will initiate restore.
        return RunPowerShell("Restore-Computer -Confirm:$false");
    }

    return JsonSerializer.Serialize(new { ok = false, code = "invalid_restore_mode" });
}

static string RunPowerShell(string command)
{
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "powershell",
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    var p = System.Diagnostics.Process.Start(psi) ?? throw new Exception("powershell_start_failed");
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();

    var ok = p.ExitCode == 0;
    return JsonSerializer.Serialize(new { ok, exitCode = p.ExitCode, stdout, stderr });
}







public sealed record InventoryResult(
    bool ok,
    string platform,
    int schemaVersion,
    int count,
    List<InventoryApp> apps,
    List<InventoryError> errors
);

public sealed record InventoryApp(
    string id,
    string name,
    string? version,
    string? publisher,
    string? installLocation,
    string? uninstall,
    string source,
    string? scope,
    string? arch,
    Dictionary<string, string?> meta
);

public sealed record InventoryError(
    string source,
    int code,
    string message
);





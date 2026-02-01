param(
  [Parameter(Mandatory=$true)][string]$Repo,
  [Parameter(Mandatory=$true)][string]$Branch,
  [Parameter(Mandatory=$true)][string]$CommitMessage,
  [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Fail([string]$m) { throw $m }
function Utf8NoBom() { return (New-Object System.Text.UTF8Encoding($false)) }
function WriteText([string]$p, [string]$c) {
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $p) | Out-Null
  [System.IO.File]::WriteAllText($p, $c, (Utf8NoBom))
}

if (-not (Test-Path -LiteralPath $Repo)) { Fail ("Missing repo: " + $Repo) }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail "git not found on PATH." }

Push-Location $Repo
try {
  if (-not (Test-Path -LiteralPath (Join-Path $Repo ".git"))) { Fail "Not a git repo root (.git missing)." }

  git checkout -B $Branch | Out-Host

  # 1) Contracts
  $contractsDir = Join-Path $Repo "src\shared\Atlas.ActivationContracts\AgentSpine"
  New-Item -ItemType Directory -Force -Path $contractsDir | Out-Null

  $helloReq = @(
    "namespace Atlas.ActivationContracts.AgentSpine;",
    "",
    "public sealed record AgentHelloRequest(",
    "    string tenantId,",
    "    string deviceId,",
    "    string os,",
    "    string arch,",
    "    string agentVersion,",
    "    string[] capabilities",
    ");",
    ""
  ) -join "`r`n"

  $helloResp = @(
    "namespace Atlas.ActivationContracts.AgentSpine;",
    "",
    "public sealed record AgentHelloResponse(",
    "    bool ok,",
    "    string code,",
    "    string? message = null",
    ");",
    ""
  ) -join "`r`n"

  WriteText (Join-Path $contractsDir "AgentHelloRequest.cs") $helloReq
  WriteText (Join-Path $contractsDir "AgentHelloResponse.cs") $helloResp

  # 2) Patch Agent Program.cs (markers + anchored insert)
  $agentProg = Join-Path $Repo "src\agent\Atlas.Agent\Program.cs"
  if (-not (Test-Path -LiteralPath $agentProg)) { Fail ("Missing agent Program.cs: " + $agentProg) }
  $src = Get-Content -Raw -LiteralPath $agentProg

  if ($src -notmatch "ATLAS_A1_HELLO_BEGIN") {
    $insert = @(
      "",
      "/* ATLAS_A1_HELLO_BEGIN */",
      "static string DetectOs()",
      "{",
      "    if (OperatingSystem.IsWindows()) return ""windows"";",
      "    if (OperatingSystem.IsMacOS()) return ""macos"";",
      "    if (OperatingSystem.IsLinux()) return ""linux"";",
      "    return ""unknown"";",
      "}",
      "",
      "static string DetectArch()",
      "{",
      "    return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();",
      "}",
      "",
      "static string DetectAgentVersion()",
      "{",
      "    return typeof(Program).Assembly.GetName().Version?.ToString() ?? ""0.0.0"";",
      "}",
      "",
      "static string[] DetectCapabilities()",
      "{",
      "    return new[] { ""inventory"" };",
      "}",
      "",
      "static async Task<bool> PostHelloAsync(HttpClient http, string baseUrl, string tenantId, string deviceId, CancellationToken ct)",
      "{",
      "    try",
      "    {",
      "        var url = $""{baseUrl}/v1/agents/hello"";",
      "        var req = new Atlas.ActivationContracts.AgentSpine.AgentHelloRequest(",
      "            tenantId: tenantId,",
      "            deviceId: deviceId,",
      "            os: DetectOs(),",
      "            arch: DetectArch(),",
      "            agentVersion: DetectAgentVersion(),",
      "            capabilities: DetectCapabilities()",
      "        );",
      "",
      "        var json = System.Text.Json.JsonSerializer.Serialize(req);",
      "        using var content = new StringContent(json, Encoding.UTF8, ""application/json"");",
      "        using var resp = await http.PostAsync(url, content, ct);",
      "        var body = await resp.Content.ReadAsStringAsync(ct);",
      "",
      "        Console.WriteLine($""HELLO status={(int)resp.StatusCode} body={body}"");",
      "        return resp.IsSuccessStatusCode;",
      "    }",
      "    catch (Exception ex)",
      "    {",
      "        Console.WriteLine(""HELLO error: "" + ex.Message);",
      "        return false;",
      "    }",
      "}",
      "/* ATLAS_A1_HELLO_END */",
      ""
    ) -join "`r`n"

    $anchor = 'Console.WriteLine($"Authority={baseUrl} TenantId={tenantId} DeviceId={deviceId} Cache={cacheDir}");'
    if ($src -notmatch [regex]::Escape($anchor)) { Fail ("Anchor not found in Program.cs: " + $anchor) }
    $src2 = $src -replace [regex]::Escape($anchor), ($anchor + "`r`n" + $insert)
    WriteText $agentProg $src2
  }
  # 3) Insert hello call immediately after Authority print line (stable anchor; no loop assumptions)
  $src = Get-Content -Raw -LiteralPath $agentProg
  if ($src -notmatch "ATLAS_A1_HELLO_CALL") {
    $callAnchor = 'Console.WriteLine($"Authority={baseUrl} TenantId={tenantId} DeviceId={deviceId} Cache={cacheDir}");'
    if ($src -notmatch [regex]::Escape($callAnchor)) { Fail ("Authority anchor not found in Program.cs: " + $callAnchor) }

    $callBlock = @(
      "/* ATLAS_A1_HELLO_CALL */",
      "await PostHelloAsync(http, baseUrl, tenantId, deviceId, cts.Token);",
      "/* ATLAS_A1_HELLO_CALL_END */",
      ""
    ) -join "`r`n"

    $src2 = $src -replace [regex]::Escape($callAnchor), ($callAnchor + "`r`n" + $callBlock)
    WriteText $agentProg $src2
  }

  # 4) Authority controller scaffold (only if missing)
  $authHello = Join-Path $Repo "src\activation\Atlas.ActivationAuthority\Controllers\AgentsController.cs"
  if (-not (Test-Path -LiteralPath $authHello)) {
    $controller = @(
      "using Microsoft.AspNetCore.Mvc;",
      "using Atlas.ActivationContracts.AgentSpine;",
      "",
      "namespace Atlas.ActivationAuthority.Controllers;",
      "",
      "[ApiController]",
      "public sealed class AgentsController : ControllerBase",
      "{",
      "    [HttpPost(""/v1/agents/hello"")]",
      "    public ActionResult<AgentHelloResponse> Hello([FromBody] AgentHelloRequest req)",
      "    {",
      "        return Ok(new AgentHelloResponse(ok: true, code: ""ok""));",
      "    }",
      "}"
    ) -join "`r`n"
    WriteText $authHello $controller
  }

  if (-not $SkipBuild) {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) { dotnet build | Out-Host }
    else { Write-Host "dotnet not found; skipping build." -ForegroundColor Yellow }
  }

  git add -A | Out-Host
  $status = git status --porcelain=v1
  if ($status) { git commit -m $CommitMessage | Out-Host }
  else { Write-Host "No changes to commit." -ForegroundColor Yellow }

  git push -u origin $Branch | Out-Host
  Write-Host ("PUSHED: " + $Branch) -ForegroundColor Green
}
finally {
  Pop-Location
}
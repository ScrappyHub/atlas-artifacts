param([Parameter(Mandatory=$true)][string]$RepoRoot)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TIER0_FULL_GREEN_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Ensure-Dir([string]$Path){
  if([string]::IsNullOrWhiteSpace($Path)){ Die "ENSURE_DIR_EMPTY" }
  if(-not (Test-Path -LiteralPath $Path -PathType Container)){
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
  }
}

function Write-Utf8NoBomLf([string]$Path,[string]$Text){
  $dir = Split-Path -Parent $Path
  if($dir){ Ensure-Dir $dir }
  $t = $Text.Replace("`r`n","`n").Replace("`r","`n")
  if(-not $t.EndsWith("`n")){ $t += "`n" }
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($Path,$t,$enc)
}

function Read-Utf8NoBom([string]$Path){
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::ReadAllText($Path,$enc)
}

function Parse-GateFile([string]$Path){
  $tok = $null
  $err = $null
  [void][System.Management.Automation.Language.Parser]::ParseFile($Path,[ref]$tok,[ref]$err)
  if($err -and $err.Count -gt 0){
    Die ("PARSE_GATE_FAIL: " + $Path + "`n" + (($err | ForEach-Object { $_.ToString() }) -join "`n"))
  }
}

function Sha256HexFile([string]$Path){
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $fs = [System.IO.File]::OpenRead($Path)
    try { $h = $sha.ComputeHash($fs) } finally { $fs.Dispose() }
  } finally {
    $sha.Dispose()
  }
  $sb = New-Object System.Text.StringBuilder
  foreach($b in $h){ [void]$sb.AppendFormat("{0:x2}", $b) }
  $sb.ToString()
}

function Invoke-ChildPwshLogged([string]$PSExe,[string]$ScriptPath,[string]$RepoRoot,[string]$BundleDir,[string]$Name){
  $stdoutPath = Join-Path $BundleDir ($Name + ".stdout.log")
  $stderrPath = Join-Path $BundleDir ($Name + ".stderr.log")
  $argList = @("-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",$ScriptPath,"-RepoRoot",$RepoRoot)
  $p = Start-Process -FilePath $PSExe -ArgumentList $argList -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
  [pscustomobject]@{ Name=$Name; ExitCode=[int]$p.ExitCode; StdoutPath=$stdoutPath; StderrPath=$stderrPath }
}

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$ScriptsDir    = Join-Path $RepoRoot "scripts"
$ProofsDir     = Join-Path $RepoRoot "proofs\receipts\atlas_tier0_full_green"
$FreezeRoot    = Join-Path $RepoRoot "test_vectors\atlas_tier0\frozen_latest_green"
$FreezePktDir  = Join-Path $FreezeRoot "packet"
$ReceiptNdjson = Join-Path $RepoRoot "proofs\receipts\atlas.ndjson"
Ensure-Dir $ScriptsDir
Ensure-Dir $ProofsDir
Ensure-Dir (Split-Path -Parent $ReceiptNdjson)

$PSExe   = (Get-Command powershell.exe -ErrorAction Stop).Source
$Dotnet  = (Get-Command dotnet.exe -ErrorAction Stop).Source
$sharedProj = Join-Path $RepoRoot "src\shared\Atlas.Handoff\Atlas.Handoff.csproj"
$cliProj    = Join-Path $RepoRoot "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj"
$required = @(
  (Join-Path $RepoRoot "scripts\_RUN_atlas_emit_inventory_smoke_v1.ps1"),
  (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_smoke_emit_and_verify_blob_v1.ps1"),
  (Join-Path $RepoRoot "scripts\_RUN_atlas_emit_inventory_packet_v1.ps1"),
  (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_packet_verify_v1.ps1"),
  (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_full_selftest_v1.ps1"),
  (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_negative_vectors_v2.ps1")
)
foreach($f in @($required)){
  if(-not (Test-Path -LiteralPath $f -PathType Leaf)){ Die ("MISSING_REQUIRED_SCRIPT: " + $f) }
  Parse-GateFile $f
  Ok ("PARSE_OK: " + $f)
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$bundle = Join-Path $ProofsDir $stamp
Ensure-Dir $bundle

Push-Location -LiteralPath $RepoRoot
try {
  & $Dotnet build $sharedProj | Out-Host
  if($LASTEXITCODE -ne 0){ Die ("DOTNET_BUILD_SHARED_FAILED exit=" + $LASTEXITCODE) }
  & $Dotnet build $cliProj | Out-Host
  if($LASTEXITCODE -ne 0){ Die ("DOTNET_BUILD_CLI_FAILED exit=" + $LASTEXITCODE) }
} finally {
  Pop-Location
}

$fullSelftest = Invoke-ChildPwshLogged -PSExe $PSExe -ScriptPath (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_full_selftest_v1.ps1") -RepoRoot $RepoRoot -BundleDir $bundle -Name "full_selftest"
$negativeVecs = Invoke-ChildPwshLogged -PSExe $PSExe -ScriptPath (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_negative_vectors_v2.ps1") -RepoRoot $RepoRoot -BundleDir $bundle -Name "negative_vectors"

$fullOut = Read-Utf8NoBom $fullSelftest.StdoutPath
$negOut  = Read-Utf8NoBom $negativeVecs.StdoutPath
if($fullSelftest.ExitCode -ne 0){ Die ("FULL_SELFTEST_FAILED exit=" + $fullSelftest.ExitCode) }
if($negativeVecs.ExitCode -ne 0){ Die ("NEGATIVE_VECTORS_FAILED exit=" + $negativeVecs.ExitCode) }
if($fullOut.IndexOf("ATLAS_TIER0_FULL_SELFTEST_OK",[StringComparison]::Ordinal) -lt 0){ Die "FULL_SELFTEST_TOKEN_MISSING" }
if($negOut.IndexOf("ATLAS_TIER0_NEGATIVE_VECTORS_OK",[StringComparison]::Ordinal) -lt 0){ Die "NEGATIVE_VECTORS_TOKEN_MISSING" }

$packetDir = $null
$lines = @($fullOut.Replace("`r`n","`n").Replace("`r","`n").Split("`n"))
foreach($line in @($lines)){
  if($line.StartsWith("PACKET_DIR=")){ $packetDir = $line.Substring("PACKET_DIR=".Length).Trim() }
}
if([string]::IsNullOrWhiteSpace($packetDir)){ Die "FULL_SELFTEST_PACKET_DIR_MISSING" }
if(-not (Test-Path -LiteralPath $packetDir -PathType Container)){ Die ("FULL_SELFTEST_PACKET_DIR_NOT_FOUND: " + $packetDir) }

if(Test-Path -LiteralPath $FreezeRoot){ Remove-Item -LiteralPath $FreezeRoot -Recurse -Force }
Ensure-Dir $FreezeRoot
Copy-Item -LiteralPath $packetDir -Destination $FreezePktDir -Recurse -Force

$freezeManifestPath = Join-Path $FreezeRoot "FREEZE_MANIFEST.txt"
$freezeStatusPath   = Join-Path $FreezeRoot "CANONICAL_STATUS.md"
$freezeManifest = @(
  "ATLAS_TIER0_FREEZE_MANIFEST_V1"
  ("UTC=" + [DateTime]::UtcNow.ToString("O"))
  ("SOURCE_PACKET_DIR=" + $packetDir)
  ("FROZEN_PACKET_DIR=" + $FreezePktDir)
  ("FULL_SELFTEST_STDOUT=" + $fullSelftest.StdoutPath)
  ("FULL_SELFTEST_STDERR=" + $fullSelftest.StderrPath)
  ("NEGATIVE_VECTORS_STDOUT=" + $negativeVecs.StdoutPath)
  ("NEGATIVE_VECTORS_STDERR=" + $negativeVecs.StderrPath)
) -join "`n"
Write-Utf8NoBomLf $freezeManifestPath $freezeManifest
$freezeStatus = @(
  "# ATLAS TIER-0 CANONICAL STATUS"
  ""
  "- Full selftest: GREEN"
  "- Negative vectors: GREEN"
  "- Evidence bundle: " + $bundle
  "- Frozen packet: " + $FreezePktDir
) -join "`n"
Write-Utf8NoBomLf $freezeStatusPath $freezeStatus

$rows = New-Object System.Collections.Generic.List[string]
$files = @(Get-ChildItem -LiteralPath $bundle -Recurse -File | Sort-Object FullName)
foreach($f in @($files)){
  $hex = Sha256HexFile $f.FullName
  $rel = $f.FullName.Substring($bundle.Length).TrimStart("\")
  [void]$rows.Add($hex + "  " + $rel)
}
Write-Utf8NoBomLf (Join-Path $bundle "sha256sums.txt") (($rows.ToArray()) -join "`n")

$receipt = [ordered]@{
  type = "atlas.tier0.full_green.v1"
  utc = [DateTime]::UtcNow.ToString("O")
  ok = $true
  repo_root = $RepoRoot
  evidence_bundle = $bundle
  frozen_packet_dir = $FreezePktDir
  full_selftest_exit = [int]$fullSelftest.ExitCode
  negative_vectors_exit = [int]$negativeVecs.ExitCode
}
$receiptLine = ($receipt | ConvertTo-Json -Compress -Depth 10)
if(Test-Path -LiteralPath $ReceiptNdjson -PathType Leaf){
  $existing = Read-Utf8NoBom $ReceiptNdjson
  $norm = $existing.Replace("`r`n","`n").Replace("`r","`n")
  if($norm.Length -gt 0 -and -not $norm.EndsWith("`n")){ $norm += "`n" }
  $norm += $receiptLine + "`n"
  Write-Utf8NoBomLf $ReceiptNdjson $norm
} else {
  Write-Utf8NoBomLf $ReceiptNdjson ($receiptLine + "`n")
}
Write-Host "ATLAS_TIER0_FULL_GREEN_OK" -ForegroundColor Green
Write-Host ("EVIDENCE_BUNDLE=" + $bundle)
Write-Host ("FROZEN_PACKET_DIR=" + $FreezePktDir)
Write-Host ("RECEIPT_PATH=" + $ReceiptNdjson)

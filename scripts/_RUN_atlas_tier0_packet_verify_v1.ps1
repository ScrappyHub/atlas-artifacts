# ============================================================================
# DEPRECATED (Atlas Tier-0). This verifier accepts BARE-HEX packet_id.txt and
# checks NO commitment/signature. It contradicts ATLAS_SPEC.md laws 7 and the
# Tier-0 theorem. Use scripts/_VERIFY_atlas_signed_inventory_packet_v1.ps1.
# Retained only for legacy/back-compat callers.
# ============================================================================
param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [string]$PacketDir = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TIER0_PACKET_VERIFY_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Read-Utf8NoBom([string]$Path){
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::ReadAllText($Path,$enc)
}

function Sha256HexBytes([byte[]]$Bytes){
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $h = $sha.ComputeHash($Bytes)
  } finally {
    $sha.Dispose()
  }
  $sb = New-Object System.Text.StringBuilder
  foreach($b in $h){ [void]$sb.AppendFormat("{0:x2}", $b) }
  $sb.ToString()
}

function Sha256HexFile([string]$Path){
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $fs = [System.IO.File]::OpenRead($Path)
    try {
      $h = $sha.ComputeHash($fs)
    } finally {
      $fs.Dispose()
    }
  } finally {
    $sha.Dispose()
  }
  $sb = New-Object System.Text.StringBuilder
  foreach($b in $h){ [void]$sb.AppendFormat("{0:x2}", $b) }
  $sb.ToString()
}

function Parse-ShaLine([string]$Line){
  $m = [regex]::Match($Line,'^(?<hex>[0-9a-f]{64})  (?<rel>.+)$')
  if(-not $m.Success){
    Die ("SHA256SUMS_BAD_LINE: " + $Line)
  }
  return @{
    hex = [string]$m.Groups["hex"].Value
    rel = [string]$m.Groups["rel"].Value
  }
}

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){
  Die ("MISSING_REPOROOT: " + $RepoRoot)
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$historyLib = Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1"
if(-not (Test-Path -LiteralPath $historyLib -PathType Leaf)){ Die ("MISSING_HISTORY_LIB: " + $historyLib) }
. $historyLib

$jobId = New-AtlasJobId "atlas_verify_packet"

if([string]::IsNullOrWhiteSpace($PacketDir)){
  $PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source
  $EmitRun = Join-Path $RepoRoot "scripts\_RUN_atlas_emit_inventory_packet_v1.ps1"
  if(-not (Test-Path -LiteralPath $EmitRun -PathType Leaf)){
    Die ("MISSING_EMIT_RUNNER: " + $EmitRun)
  }

  $emitOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $EmitRun -RepoRoot $RepoRoot 2>&1
  foreach($x in @($emitOut)){ [Console]::Out.WriteLine([string]$x) }
  if($LASTEXITCODE -ne 0){
    Die ("EMIT_PACKET_FAILED exit=" + $LASTEXITCODE)
  }

  foreach($x in @($emitOut)){
    if(($x -is [string]) -and $x.StartsWith("PACKET_DIR=")){
      $PacketDir = $x.Substring("PACKET_DIR=".Length).Trim()
    }
  }
  if([string]::IsNullOrWhiteSpace($PacketDir)){
    Die "MISSING_PACKET_DIR_LINE"
  }
}

if(-not (Test-Path -LiteralPath $PacketDir -PathType Container)){
  Die ("PACKET_DIR_MISSING: " + $PacketDir)
}
$PacketDir = (Resolve-Path -LiteralPath $PacketDir).Path
Ok ("PACKET_DIR=" + $PacketDir)

$manifestPath = Join-Path $PacketDir "manifest.json"
$packetIdPath = Join-Path $PacketDir "packet_id.txt"
$shaPath      = Join-Path $PacketDir "sha256sums.txt"

if(-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)){ Die ("MISSING_MANIFEST: " + $manifestPath) }
if(-not (Test-Path -LiteralPath $packetIdPath -PathType Leaf)){ Die ("MISSING_PACKET_ID_TXT: " + $packetIdPath) }
if(-not (Test-Path -LiteralPath $shaPath -PathType Leaf)){ Die ("MISSING_SHA256SUMS: " + $shaPath) }

$manifestSha = Sha256HexFile $manifestPath

$packetIdRaw  = Read-Utf8NoBom $packetIdPath
$packetIdTrim = $packetIdRaw.Trim()

if([string]::IsNullOrWhiteSpace($packetIdTrim)){ Die "PACKET_ID_TXT_EMPTY" }
if($packetIdTrim.Length -ne 64){ Die ("PACKET_ID_TXT_BAD_LEN: " + $packetIdTrim.Length) }
if($packetIdTrim -notmatch '^[0-9a-f]{64}$'){ Die "PACKET_ID_TXT_NOT_HEX64" }
if($packetIdTrim -ne $manifestSha){
  Die ("PACKET_ID_TXT_MISMATCH: got=" + $packetIdTrim + " expected=" + $manifestSha)
}

$manifestRaw = Read-Utf8NoBom $manifestPath
$refMatch = [regex]::Match($manifestRaw,'"content_ref":"sha256:(?<hex>[0-9a-f]{64})"')
if(-not $refMatch.Success){
  Die "CONTENT_REF_MISSING_IN_MANIFEST"
}
$contentHex = $refMatch.Groups["hex"].Value
$contentRef = "sha256:" + $contentHex
$blobPath = Join-Path $RepoRoot ("data\blobs\" + $contentHex)
if(-not (Test-Path -LiteralPath $blobPath -PathType Leaf)){
  Die ("MISSING_BLOB: " + $blobPath)
}

$shaRaw = Read-Utf8NoBom $shaPath
$shaNorm = $shaRaw.Replace("`r`n","`n").Replace("`r","`n")
if(-not $shaNorm.EndsWith("`n")){
  Die "SHA256SUMS_MUST_END_WITH_LF"
}

$allLines = @($shaNorm.Split("`n"))
$nonEmptyLines = @()
foreach($line in @($allLines)){
  if(-not [string]::IsNullOrWhiteSpace($line)){
    $nonEmptyLines += $line
  }
}
if($nonEmptyLines.Count -lt 1){
  Die "SHA256SUMS_EMPTY"
}

$selfHex = $null
$preSelfLines = @()

foreach($line in @($nonEmptyLines)){
  $row = Parse-ShaLine $line
  $rel = [string]$row["rel"]
  $hex = [string]$row["hex"]

  if($rel -eq "sha256sums.txt"){
    $selfHex = $hex
    continue
  }

  $targetPath = Join-Path $PacketDir $rel
  if(-not (Test-Path -LiteralPath $targetPath -PathType Leaf)){
    Die ("MISSING_PAYLOAD_FILE: " + $rel)
  }

  $got = Sha256HexFile $targetPath
  if($got -ne $hex){
    Die ("SHA256SUMS_HASH_MISMATCH: rel=" + $rel + " got=" + $got + " expected=" + $hex)
  }

  $preSelfLines += ($hex + "  " + $rel)
}

if([string]::IsNullOrWhiteSpace($selfHex)){
  Die "SHA256SUMS_SELF_ROW_MISSING"
}

$preSelfText = ""
if($preSelfLines.Count -gt 0){
  $preSelfText = (($preSelfLines -join "`n") + "`n")
} else {
  $preSelfText = "`n"
}

$enc = New-Object System.Text.UTF8Encoding($false)
$reconstructedSelfHex = Sha256HexBytes ($enc.GetBytes($preSelfText))

if($reconstructedSelfHex -ne $selfHex){
  Die ("SHA256SUMS_HASH_MISMATCH: rel=sha256sums.txt got=" + $reconstructedSelfHex + " expected=" + $selfHex)
}

$null = Add-AtlasJobLedgerLine -RepoRoot $RepoRoot -JobId $jobId -JobType "verify-inventory-packet" -Status "ok" -DeviceId "" -ContentRef $contentRef -PacketId $packetIdTrim -PacketDir $PacketDir -Note "verify packet completed"

Write-Host "ATLAS_TIER0_PACKET_VERIFY_OK" -ForegroundColor Green
Write-Host ("JOB_ID=" + $jobId)
Write-Host ("PACKET_DIR=" + $PacketDir)
Write-Host ("PACKET_ID=" + $packetIdTrim)
Write-Host ("MANIFEST_SHA256=" + $manifestSha)

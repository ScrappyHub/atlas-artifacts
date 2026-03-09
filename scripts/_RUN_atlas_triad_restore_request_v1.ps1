param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$ReferencePath,
  [string]$RestoreTarget = "device/dev-1/request-target-a",
  [string]$RequestReason = "triad restore request"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_RESTORE_REQUEST_FAIL: " + $m) }

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
if(-not (Test-Path -LiteralPath $ReferencePath -PathType Leaf)){ Die ("MISSING_REFERENCE_PATH: " + $ReferencePath) }
$ReferencePath = (Resolve-Path -LiteralPath $ReferencePath).Path

$historyLib = Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1"
$refLib     = Join-Path $RepoRoot "scripts\_lib_atlas_triad_boundary_v1.ps1"
$intentLib  = Join-Path $RepoRoot "scripts\_lib_atlas_triad_intents_v1.ps1"

foreach($p in @($historyLib,$refLib,$intentLib)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_REQUIRED_LIB: " + $p) }
}

. $historyLib
. $refLib
. $intentLib

$raw = Read-Utf8NoBom $ReferencePath
try {
  $refObj = $raw | ConvertFrom-Json -ErrorAction Stop
}
catch {
  Die ("REFERENCE_JSON_PARSE_FAIL: " + $_.Exception.Message)
}

[void](Test-AtlasTriadReferenceV1 $refObj)

$reqObj = [ordered]@{
  schema             = "atlas.triad.restore_request.v1"
  triad_ref          = [string]$refObj.triad_ref
  snapshot_ref       = [string]$refObj.snapshot_ref
  source_packet_id   = [string]$refObj.source_packet_id
  source_content_ref = [string]$refObj.source_content_ref
  device_id          = [string]$refObj.device_id
  captured_utc       = [string]$refObj.captured_utc
  restore_target     = $RestoreTarget
  request_reason     = $RequestReason
  note               = "request derived from atlas.triad.reference.v1"
}

$jobId = New-AtlasJobId "atlas_triad_restore_request"
$paths = Get-AtlasTriadIntentPaths $RepoRoot
$requestPath = Add-AtlasTriadRestoreRequestLine -RepoRoot $RepoRoot -JobId $jobId -Obj $reqObj

$null = Add-AtlasJobLedgerLine `
  -RepoRoot $RepoRoot `
  -JobId $jobId `
  -JobType "triad-restore-request" `
  -Status "ok" `
  -DeviceId ([string]$reqObj.device_id) `
  -ContentRef ([string]$reqObj.source_content_ref) `
  -PacketId ([string]$reqObj.source_packet_id) `
  -PacketDir "" `
  -Note ("triad_ref=" + [string]$reqObj.triad_ref + "; restore_target=" + [string]$reqObj.restore_target)

Write-Host "ATLAS_TRIAD_RESTORE_REQUEST_OK" -ForegroundColor Green
Write-Host ("JOB_ID=" + $jobId)
Write-Host ("RESTORE_REQUEST_PATH=" + $requestPath)
Write-Host ("JOB_LEDGER_PATH=" + $paths.JobLedgerPath)

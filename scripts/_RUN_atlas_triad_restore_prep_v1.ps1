param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$ReferencePath,
  [string]$RestoreTarget = "device-restore-default",
  [string]$PrepReason = "triad restore preparation"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_RESTORE_PREP_FAIL: " + $m) }

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

if(-not (Test-Path -LiteralPath $ReferencePath -PathType Leaf)){ Die ("MISSING_REFERENCE_PATH: " + $ReferencePath) }
$ReferencePath = (Resolve-Path -LiteralPath $ReferencePath).Path

$historyLib = Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1"
$refLib     = Join-Path $RepoRoot "scripts\_lib_atlas_triad_boundary_v1.ps1"
$prepLib    = Join-Path $RepoRoot "scripts\_lib_atlas_triad_prep_boundary_v1.ps1"

foreach($p in @($historyLib,$refLib,$prepLib)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_REQUIRED_LIB: " + $p) }
}

. $historyLib
. $refLib
. $prepLib

$raw = Read-Utf8NoBom $ReferencePath
try {
  $refObj = $raw | ConvertFrom-Json -ErrorAction Stop
}
catch {
  Die ("REFERENCE_JSON_PARSE_FAIL: " + $_.Exception.Message)
}

[void](Test-AtlasTriadReferenceV1 $refObj)

if(([string]$refObj.handoff_kind) -ne "restore-prep"){
  Die ("HANDOFF_KIND_NOT_RESTORE_PREP: " + [string]$refObj.handoff_kind)
}

$prepObj = [pscustomobject][ordered]@{
  schema             = "atlas.triad.restore_prep.v1"
  triad_ref          = [string]$refObj.triad_ref
  snapshot_ref       = [string]$refObj.snapshot_ref
  source_packet_id   = [string]$refObj.source_packet_id
  source_content_ref = [string]$refObj.source_content_ref
  device_id          = [string]$refObj.device_id
  captured_utc       = [string]$refObj.captured_utc
  restore_target     = $RestoreTarget
  prep_reason        = $PrepReason
  note               = "prepared from atlas.triad.reference.v1"
}

[void](Test-AtlasTriadRestorePrepV1 $prepObj)

$jobId = New-AtlasJobId "atlas_triad_restore_prep"
$paths = Get-AtlasTriadPrepPaths $RepoRoot
$restorePrepPath = Add-AtlasTriadRestorePrepLine -RepoRoot $RepoRoot -JobId $jobId -PrepObj $prepObj

$null = Add-AtlasJobLedgerLine `
  -RepoRoot $RepoRoot `
  -JobId $jobId `
  -JobType "triad-restore-prep" `
  -Status "ok" `
  -DeviceId ([string]$prepObj.device_id) `
  -ContentRef ([string]$prepObj.source_content_ref) `
  -PacketId ([string]$prepObj.source_packet_id) `
  -PacketDir "" `
  -Note ("triad_ref=" + [string]$prepObj.triad_ref + "; restore_target=" + [string]$prepObj.restore_target)

Write-Host "ATLAS_TRIAD_RESTORE_PREP_OK" -ForegroundColor Green
Write-Host ("JOB_ID=" + $jobId)
Write-Host ("RESTORE_PREP_PATH=" + $restorePrepPath)
Write-Host ("JOB_LEDGER_PATH=" + $paths.JobLedgerPath)
Write-Host ("TRIAD_REF=" + [string]$prepObj.triad_ref)
Write-Host ("SNAPSHOT_REF=" + [string]$prepObj.snapshot_ref)
Write-Host ("RESTORE_TARGET=" + [string]$prepObj.restore_target)

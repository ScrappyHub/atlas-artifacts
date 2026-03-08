param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$ReferencePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_REFERENCE_INTAKE_FAIL: " + $m) }

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

if(-not (Test-Path -LiteralPath $ReferencePath -PathType Leaf)){ Die ("MISSING_REFERENCE_PATH: " + $ReferencePath) }
$ReferencePath = (Resolve-Path -LiteralPath $ReferencePath).Path

$historyLib = Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1"
$triadLib   = Join-Path $RepoRoot "scripts\_lib_atlas_triad_boundary_v1.ps1"

if(-not (Test-Path -LiteralPath $historyLib -PathType Leaf)){ Die ("MISSING_HISTORY_LIB: " + $historyLib) }
if(-not (Test-Path -LiteralPath $triadLib -PathType Leaf)){ Die ("MISSING_TRIAD_LIB: " + $triadLib) }

. $historyLib
. $triadLib

$paths = Get-AtlasTriadBoundaryPaths $RepoRoot
$raw = Read-Utf8NoBom $ReferencePath

try {
  $obj = $raw | ConvertFrom-Json -ErrorAction Stop
}
catch {
  Die ("REFERENCE_JSON_PARSE_FAIL: " + $_.Exception.Message)
}

[void](Test-AtlasTriadReferenceV1 $obj)

$jobId = New-AtlasJobId "atlas_triad_intake"
$triadRefsPath = Add-AtlasTriadReferenceIntakeLine -RepoRoot $RepoRoot -JobId $jobId -ReferenceObj $obj

$null = Add-AtlasJobLedgerLine `
  -RepoRoot $RepoRoot `
  -JobId $jobId `
  -JobType "triad-reference-intake" `
  -Status "ok" `
  -DeviceId ([string]$obj.device_id) `
  -ContentRef ([string]$obj.source_content_ref) `
  -PacketId ([string]$obj.source_packet_id) `
  -PacketDir "" `
  -Note ("triad_ref=" + [string]$obj.triad_ref + "; handoff_kind=" + [string]$obj.handoff_kind)

Write-Host "ATLAS_TRIAD_REFERENCE_INTAKE_OK" -ForegroundColor Green
Write-Host ("JOB_ID=" + $jobId)
Write-Host ("TRIAD_REFS_PATH=" + $triadRefsPath)
Write-Host ("JOB_LEDGER_PATH=" + $paths.JobLedgerPath)
Write-Host ("TRIAD_REF=" + [string]$obj.triad_ref)
Write-Host ("SNAPSHOT_REF=" + [string]$obj.snapshot_ref)
Write-Host ("HANDOFF_KIND=" + [string]$obj.handoff_kind)

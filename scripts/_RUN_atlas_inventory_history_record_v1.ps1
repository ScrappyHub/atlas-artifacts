param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$JobId,
  [Parameter(Mandatory=$true)][string]$DeviceId,
  [Parameter(Mandatory=$true)][string]$ContentRef,
  [Parameter(Mandatory=$true)][string]$BlobPath,
  [Parameter(Mandatory=$true)][string]$CapturedUtc,
  [string]$Source = "atlas.inventory.snapshot.v1"
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
. (Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1")
$p = Add-AtlasInventoryHistoryLine -RepoRoot $RepoRoot -JobId $JobId -DeviceId $DeviceId -ContentRef $ContentRef -BlobPath $BlobPath -CapturedUtc $CapturedUtc -Source $Source
Write-Host "ATLAS_INVENTORY_HISTORY_RECORD_OK" -ForegroundColor Green
Write-Host ("INVENTORY_HISTORY_PATH=" + $p)
Write-Host ("JOB_ID=" + $JobId)

param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$JobId,
  [Parameter(Mandatory=$true)][string]$JobType,
  [Parameter(Mandatory=$true)][string]$Status,
  [string]$DeviceId = "",
  [string]$ContentRef = "",
  [string]$PacketId = "",
  [string]$PacketDir = "",
  [string]$Note = ""
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
. (Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1")
$p = Add-AtlasJobLedgerLine -RepoRoot $RepoRoot -JobId $JobId -JobType $JobType -Status $Status -DeviceId $DeviceId -ContentRef $ContentRef -PacketId $PacketId -PacketDir $PacketDir -Note $Note
Write-Host "ATLAS_JOB_LEDGER_RECORD_OK" -ForegroundColor Green
Write-Host ("JOB_LEDGER_PATH=" + $p)
Write-Host ("JOB_ID=" + $JobId)

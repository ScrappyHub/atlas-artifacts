param([Parameter(Mandatory=$true)][string]$RepoRoot)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
function Die([string]$m){ throw ("ATLAS_HISTORY_JOB_SMOKE_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source
$EmitSmoke = Join-Path $RepoRoot "scripts\_RUN_atlas_emit_inventory_smoke_v1.ps1"
$JobRun    = Join-Path $RepoRoot "scripts\_RUN_atlas_job_ledger_record_v1.ps1"
$HistRun   = Join-Path $RepoRoot "scripts\_RUN_atlas_inventory_history_record_v1.ps1"
. (Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1")
$jobId = New-AtlasJobId "atlas_histjob"
$emitOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $EmitSmoke -RepoRoot $RepoRoot 2>&1
foreach($x in @($emitOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("EMIT_SMOKE_FAILED exit=" + $LASTEXITCODE) }
$contentRef = $null
$blobPath = $null
$capturedUtc = $null
$deviceId = $null
foreach($x in @($emitOut)){
  $s = [string]$x
  if($s.StartsWith("CONTENT_REF=")){ $contentRef = $s.Substring("CONTENT_REF=".Length).Trim() }
  elseif($s.StartsWith("BLOB_PATH=")){ $blobPath = $s.Substring("BLOB_PATH=".Length).Trim() }
  elseif($s.StartsWith("CAPTURED_UTC=")){ $capturedUtc = $s.Substring("CAPTURED_UTC=".Length).Trim() }
  elseif($s.StartsWith("DEVICE_ID=")){ $deviceId = $s.Substring("DEVICE_ID=".Length).Trim() }
}
if([string]::IsNullOrWhiteSpace($contentRef)){ Die "MISSING_CONTENT_REF" }
if([string]::IsNullOrWhiteSpace($blobPath)){ Die "MISSING_BLOB_PATH" }
if([string]::IsNullOrWhiteSpace($capturedUtc)){ Die "MISSING_CAPTURED_UTC" }
if([string]::IsNullOrWhiteSpace($deviceId)){ Die "MISSING_DEVICE_ID" }
$histOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $HistRun -RepoRoot $RepoRoot -JobId $jobId -DeviceId $deviceId -ContentRef $contentRef -BlobPath $blobPath -CapturedUtc $capturedUtc -Source "atlas.inventory.snapshot.v1" 2>&1
foreach($x in @($histOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("HISTORY_RECORD_FAILED exit=" + $LASTEXITCODE) }
$jobOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $JobRun -RepoRoot $RepoRoot -JobId $jobId -JobType "emit-inventory" -Status "ok" -DeviceId $deviceId -ContentRef $contentRef -Note "history-job-smoke" 2>&1
foreach($x in @($jobOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("JOB_RECORD_FAILED exit=" + $LASTEXITCODE) }
$paths = Get-AtlasDataPaths $RepoRoot
if(-not (Test-Path -LiteralPath $paths.InventoryHistoryPath -PathType Leaf)){ Die "INVENTORY_HISTORY_FILE_MISSING" }
if(-not (Test-Path -LiteralPath $paths.JobLedgerPath -PathType Leaf)){ Die "JOB_LEDGER_FILE_MISSING" }
$histTxt = Read-Utf8NoBom $paths.InventoryHistoryPath
$jobTxt  = Read-Utf8NoBom $paths.JobLedgerPath
if($histTxt.IndexOf($jobId,[StringComparison]::Ordinal) -lt 0){ Die "JOB_ID_NOT_FOUND_IN_HISTORY" }
if($jobTxt.IndexOf($jobId,[StringComparison]::Ordinal) -lt 0){ Die "JOB_ID_NOT_FOUND_IN_JOB_LEDGER" }
Ok ("INVENTORY_HISTORY_PATH=" + $paths.InventoryHistoryPath)
Ok ("JOB_LEDGER_PATH=" + $paths.JobLedgerPath)
Write-Host "ATLAS_HISTORY_JOB_SURFACE_SMOKE_OK" -ForegroundColor Green

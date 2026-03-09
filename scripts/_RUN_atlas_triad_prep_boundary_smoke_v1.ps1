param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_PREP_BOUNDARY_SMOKE_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Write-Utf8NoBomLf([string]$Path,[string]$Text){
  $dir = Split-Path -Parent $Path
  if($dir -and -not (Test-Path -LiteralPath $dir -PathType Container)){
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
  }
  $t = $Text.Replace("`r`n","`n").Replace("`r","`n")
  if(-not $t.EndsWith("`n")){ $t += "`n" }
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($Path,$t,$enc)
}

function Read-Utf8NoBom([string]$Path){
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::ReadAllText($Path,$enc)
}

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$prepLib    = Join-Path $RepoRoot "scripts\_lib_atlas_triad_prep_boundary_v1.ps1"
$restoreRun = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_restore_prep_v1.ps1"
$updateRun  = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_update_prep_v1.ps1"
$tvDir      = Join-Path $RepoRoot "test_vectors\atlas_triad_prep_boundary"
$restoreRef = Join-Path $tvDir "minimal_restore_reference.v1.json"
$updateRef  = Join-Path $tvDir "minimal_update_reference.v1.json"

foreach($p in @($prepLib,$restoreRun,$updateRun)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_REQUIRED_PATH: " + $p) }
}

. $prepLib

if(-not (Test-Path -LiteralPath $tvDir -PathType Container)){
  New-Item -ItemType Directory -Force -Path $tvDir | Out-Null
}

$restoreRefObj = [ordered]@{
  schema             = "atlas.triad.reference.v1"
  triad_ref          = "triad://restore-set/restore-prep-minimal-v1"
  snapshot_ref       = "snapshot://device/dev-1/restore/2026-03-08T00:00:00Z"
  source_packet_id   = "atlaspacket_restore_prep_minimal_v1"
  source_content_ref = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
  device_id          = "dev-1"
  captured_utc       = "2026-03-08T00:00:00Z"
  handoff_kind       = "restore-prep"
  note               = "minimal restore prep vector"
}
$updateRefObj = [ordered]@{
  schema             = "atlas.triad.reference.v1"
  triad_ref          = "triad://restore-set/update-prep-minimal-v1"
  snapshot_ref       = "snapshot://device/dev-1/update/2026-03-08T00:00:00Z"
  source_packet_id   = "atlaspacket_update_prep_minimal_v1"
  source_content_ref = "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
  device_id          = "dev-1"
  captured_utc       = "2026-03-08T00:00:00Z"
  handoff_kind       = "update-prep"
  note               = "minimal update prep vector"
}

Write-Utf8NoBomLf $restoreRef (($restoreRefObj | ConvertTo-Json -Compress -Depth 10))
Write-Utf8NoBomLf $updateRef  (($updateRefObj  | ConvertTo-Json -Compress -Depth 10))
Ok ("WROTE_VECTOR=" + $restoreRef)
Ok ("WROTE_VECTOR=" + $updateRef)

$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source

$restoreOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $restoreRun -RepoRoot $RepoRoot -ReferencePath $restoreRef -RestoreTarget "device/dev-1/restore-target-a" -PrepReason "triad restore prep smoke" 2>&1
foreach($x in @($restoreOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("RESTORE_PREP_FAILED exit=" + $LASTEXITCODE) }

$updateOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $updateRun -RepoRoot $RepoRoot -ReferencePath $updateRef -ArtifactTarget "atlas/artifacts/update-target-a" -PrepReason "triad update prep smoke" 2>&1
foreach($x in @($updateOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("UPDATE_PREP_FAILED exit=" + $LASTEXITCODE) }

$restoreJoined = (@($restoreOut) -join "`n")
$updateJoined  = (@($updateOut)  -join "`n")

if($restoreJoined.IndexOf("ATLAS_TRIAD_RESTORE_PREP_OK",[StringComparison]::Ordinal) -lt 0){
  Die "RESTORE_PREP_TOKEN_MISSING"
}
if($updateJoined.IndexOf("ATLAS_TRIAD_UPDATE_PREP_OK",[StringComparison]::Ordinal) -lt 0){
  Die "UPDATE_PREP_TOKEN_MISSING"
}

$paths = Get-AtlasTriadPrepPaths $RepoRoot
if(-not (Test-Path -LiteralPath $paths.RestorePrepPath -PathType Leaf)){ Die ("RESTORE_PREP_PATH_MISSING: " + $paths.RestorePrepPath) }
if(-not (Test-Path -LiteralPath $paths.UpdatePrepPath -PathType Leaf)){ Die ("UPDATE_PREP_PATH_MISSING: " + $paths.UpdatePrepPath) }
if(-not (Test-Path -LiteralPath $paths.JobLedgerPath -PathType Leaf)){ Die ("JOB_LEDGER_PATH_MISSING: " + $paths.JobLedgerPath) }

$restoreRaw = Read-Utf8NoBom $paths.RestorePrepPath
$updateRaw  = Read-Utf8NoBom $paths.UpdatePrepPath
$jobsRaw    = Read-Utf8NoBom $paths.JobLedgerPath

if($restoreRaw.IndexOf('"type":"atlas.triad.restore_prep.record.v1"',[StringComparison]::Ordinal) -lt 0){
  Die "RESTORE_PREP_APPEND_MISSING"
}
if($updateRaw.IndexOf('"type":"atlas.triad.update_prep.record.v1"',[StringComparison]::Ordinal) -lt 0){
  Die "UPDATE_PREP_APPEND_MISSING"
}
if($jobsRaw.IndexOf('"job_type":"triad-restore-prep"',[StringComparison]::Ordinal) -lt 0){
  Die "RESTORE_JOB_LEDGER_APPEND_MISSING"
}
if($jobsRaw.IndexOf('"job_type":"triad-update-prep"',[StringComparison]::Ordinal) -lt 0){
  Die "UPDATE_JOB_LEDGER_APPEND_MISSING"
}

Ok ("RESTORE_PREP_PATH=" + $paths.RestorePrepPath)
Ok ("UPDATE_PREP_PATH=" + $paths.UpdatePrepPath)
Ok ("JOB_LEDGER_PATH=" + $paths.JobLedgerPath)
Write-Host "ATLAS_TRIAD_PREP_BOUNDARY_SMOKE_OK" -ForegroundColor Green

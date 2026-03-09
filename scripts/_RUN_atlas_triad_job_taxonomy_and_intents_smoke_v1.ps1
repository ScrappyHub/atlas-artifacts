param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_JOB_TAXONOMY_AND_INTENTS_SMOKE_FAIL: " + $m) }
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

$intentLib   = Join-Path $RepoRoot "scripts\_lib_atlas_triad_intents_v1.ps1"
$restoreRun  = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_restore_request_v1.ps1"
$applyRun    = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_artifact_apply_request_v1.ps1"
$tvDir       = Join-Path $RepoRoot "test_vectors\atlas_triad_intents"
$restoreRef  = Join-Path $tvDir "minimal_restore_request_reference.v1.json"
$applyRef    = Join-Path $tvDir "minimal_artifact_apply_request_reference.v1.json"

foreach($p in @($intentLib,$restoreRun,$applyRun)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_REQUIRED_PATH: " + $p) }
}

. $intentLib

if(-not (Test-Path -LiteralPath $tvDir -PathType Container)){
  New-Item -ItemType Directory -Force -Path $tvDir | Out-Null
}

$restoreRefObj = [ordered]@{
  schema             = "atlas.triad.reference.v1"
  triad_ref          = "triad://intent/restore-request-minimal-v1"
  snapshot_ref       = "snapshot://device/dev-1/restore-request/2026-03-08T00:00:00Z"
  source_packet_id   = "atlaspacket_restore_request_minimal_v1"
  source_content_ref = "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
  device_id          = "dev-1"
  captured_utc       = "2026-03-08T00:00:00Z"
  handoff_kind       = "restore-prep"
  note               = "minimal restore request vector"
}
$applyRefObj = [ordered]@{
  schema             = "atlas.triad.reference.v1"
  triad_ref          = "triad://intent/artifact-apply-request-minimal-v1"
  snapshot_ref       = "snapshot://device/dev-1/artifact-request/2026-03-08T00:00:00Z"
  source_packet_id   = "atlaspacket_artifact_apply_request_minimal_v1"
  source_content_ref = "sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
  device_id          = "dev-1"
  captured_utc       = "2026-03-08T00:00:00Z"
  handoff_kind       = "update-prep"
  note               = "minimal artifact apply request vector"
}

Write-Utf8NoBomLf $restoreRef (($restoreRefObj | ConvertTo-Json -Compress -Depth 10))
Write-Utf8NoBomLf $applyRef  (($applyRefObj  | ConvertTo-Json -Compress -Depth 10))
Ok ("WROTE_VECTOR=" + $restoreRef)
Ok ("WROTE_VECTOR=" + $applyRef)

$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source

$restoreOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $restoreRun -RepoRoot $RepoRoot -ReferencePath $restoreRef -RestoreTarget "device/dev-1/restore-request-target-a" -RequestReason "triad restore request smoke" 2>&1
foreach($x in @($restoreOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("RESTORE_REQUEST_FAILED exit=" + $LASTEXITCODE) }

$applyOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $applyRun -RepoRoot $RepoRoot -ReferencePath $applyRef -ArtifactTarget "atlas/artifacts/apply-request-target-a" -RequestReason "triad artifact apply request smoke" 2>&1
foreach($x in @($applyOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("ARTIFACT_APPLY_REQUEST_FAILED exit=" + $LASTEXITCODE) }

$restoreJoined = (@($restoreOut) -join "`n")
$applyJoined   = (@($applyOut)  -join "`n")

if($restoreJoined.IndexOf("ATLAS_TRIAD_RESTORE_REQUEST_OK",[StringComparison]::Ordinal) -lt 0){
  Die "RESTORE_REQUEST_TOKEN_MISSING"
}
if($applyJoined.IndexOf("ATLAS_TRIAD_ARTIFACT_APPLY_REQUEST_OK",[StringComparison]::Ordinal) -lt 0){
  Die "ARTIFACT_APPLY_REQUEST_TOKEN_MISSING"
}

$paths = Get-AtlasTriadIntentPaths $RepoRoot
if(-not (Test-Path -LiteralPath $paths.RestoreRequestPath -PathType Leaf)){ Die ("RESTORE_REQUEST_PATH_MISSING: " + $paths.RestoreRequestPath) }
if(-not (Test-Path -LiteralPath $paths.ArtifactApplyRequestPath -PathType Leaf)){ Die ("ARTIFACT_APPLY_REQUEST_PATH_MISSING: " + $paths.ArtifactApplyRequestPath) }
if(-not (Test-Path -LiteralPath $paths.JobLedgerPath -PathType Leaf)){ Die ("JOB_LEDGER_PATH_MISSING: " + $paths.JobLedgerPath) }

$restoreRaw = Read-Utf8NoBom $paths.RestoreRequestPath
$applyRaw   = Read-Utf8NoBom $paths.ArtifactApplyRequestPath
$jobsRaw    = Read-Utf8NoBom $paths.JobLedgerPath

if($restoreRaw.IndexOf('"type":"atlas.triad.restore_request.record.v1"',[StringComparison]::Ordinal) -lt 0){
  Die "RESTORE_REQUEST_APPEND_MISSING"
}
if($applyRaw.IndexOf('"type":"atlas.triad.artifact_apply_request.record.v1"',[StringComparison]::Ordinal) -lt 0){
  Die "ARTIFACT_APPLY_REQUEST_APPEND_MISSING"
}
if($jobsRaw.IndexOf('"job_type":"triad-restore-request"',[StringComparison]::Ordinal) -lt 0){
  Die "RESTORE_REQUEST_JOB_APPEND_MISSING"
}
if($jobsRaw.IndexOf('"job_type":"triad-artifact-apply-request"',[StringComparison]::Ordinal) -lt 0){
  Die "ARTIFACT_APPLY_REQUEST_JOB_APPEND_MISSING"
}

Ok ("RESTORE_REQUEST_PATH=" + $paths.RestoreRequestPath)
Ok ("ARTIFACT_APPLY_REQUEST_PATH=" + $paths.ArtifactApplyRequestPath)
Ok ("JOB_LEDGER_PATH=" + $paths.JobLedgerPath)
Write-Host "ATLAS_TRIAD_JOB_TAXONOMY_AND_INTENTS_SMOKE_OK" -ForegroundColor Green

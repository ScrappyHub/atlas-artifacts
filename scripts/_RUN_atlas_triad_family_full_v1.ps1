param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_FAMILY_FULL_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Read-Utf8NoBom([string]$Path){
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::ReadAllText($Path,$enc)
}

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$refLib      = Join-Path $RepoRoot "scripts\_lib_atlas_triad_boundary_v1.ps1"
$prepLib     = Join-Path $RepoRoot "scripts\_lib_atlas_triad_prep_boundary_v1.ps1"
$intentLib   = Join-Path $RepoRoot "scripts\_lib_atlas_triad_intents_v1.ps1"
$familyLib   = Join-Path $RepoRoot "scripts\_lib_atlas_triad_family_v1.ps1"

$refSmoke    = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_boundary_smoke_v1.ps1"
$prepSmoke   = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_prep_boundary_smoke_v1.ps1"
$intentSmoke = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_job_taxonomy_and_intents_smoke_v1.ps1"

foreach($p in @($refLib,$prepLib,$intentLib,$familyLib,$refSmoke,$prepSmoke,$intentSmoke)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_REQUIRED_PATH: " + $p) }
}

. $refLib
. $prepLib
. $intentLib
. $familyLib

$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source

$refOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $refSmoke -RepoRoot $RepoRoot 2>&1
foreach($x in @($refOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("REFERENCE_SMOKE_FAILED exit=" + $LASTEXITCODE) }

$prepOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $prepSmoke -RepoRoot $RepoRoot 2>&1
foreach($x in @($prepOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("PREP_SMOKE_FAILED exit=" + $LASTEXITCODE) }

$intentOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $intentSmoke -RepoRoot $RepoRoot 2>&1
foreach($x in @($intentOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){ Die ("INTENT_SMOKE_FAILED exit=" + $LASTEXITCODE) }

$refVector      = Join-Path $RepoRoot "test_vectors\atlas_triad_boundary\minimal_reference.v1.json"
$restorePrepVec = Join-Path $RepoRoot "test_vectors\atlas_triad_prep_boundary\minimal_restore_reference.v1.json"
$updatePrepVec  = Join-Path $RepoRoot "test_vectors\atlas_triad_prep_boundary\minimal_update_reference.v1.json"
$restoreReqVec  = Join-Path $RepoRoot "test_vectors\atlas_triad_intents\minimal_restore_request_reference.v1.json"
$applyReqVec    = Join-Path $RepoRoot "test_vectors\atlas_triad_intents\minimal_artifact_apply_request_reference.v1.json"

foreach($p in @($refVector,$restorePrepVec,$updatePrepVec,$restoreReqVec,$applyReqVec)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_VECTOR: " + $p) }
}

function Validate-JsonVector([string]$Path){
  $raw = Read-Utf8NoBom $Path
  try {
    $obj = $raw | ConvertFrom-Json -ErrorAction Stop
  }
  catch {
    Die ("VECTOR_JSON_PARSE_FAIL: " + $Path + " :: " + $_.Exception.Message)
  }
  $class = Test-AtlasTriadFamilyObject $obj
  Ok ("FAMILY_OBJECT_OK: " + $Path + " :: " + $class)
}

Validate-JsonVector $refVector
Validate-JsonVector $restorePrepVec
Validate-JsonVector $updatePrepVec
Validate-JsonVector $restoreReqVec
Validate-JsonVector $applyReqVec

$refPaths = Get-AtlasTriadBoundaryPaths $RepoRoot
$prepPaths = Get-AtlasTriadPrepPaths $RepoRoot
$intentPaths = Get-AtlasTriadIntentPaths $RepoRoot

foreach($p in @($refPaths.TriadRefsPath,$prepPaths.RestorePrepPath,$prepPaths.UpdatePrepPath,$intentPaths.RestoreRequestPath,$intentPaths.ArtifactApplyRequestPath,$intentPaths.JobLedgerPath)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_APPEND_ONLY_SURFACE: " + $p) }
}

Ok ("TRIAD_REFS_PATH=" + $refPaths.TriadRefsPath)
Ok ("RESTORE_PREP_PATH=" + $prepPaths.RestorePrepPath)
Ok ("UPDATE_PREP_PATH=" + $prepPaths.UpdatePrepPath)
Ok ("RESTORE_REQUEST_PATH=" + $intentPaths.RestoreRequestPath)
Ok ("ARTIFACT_APPLY_REQUEST_PATH=" + $intentPaths.ArtifactApplyRequestPath)
Ok ("JOB_LEDGER_PATH=" + $intentPaths.JobLedgerPath)
Write-Host "ATLAS_TRIAD_FAMILY_FULL_OK" -ForegroundColor Green

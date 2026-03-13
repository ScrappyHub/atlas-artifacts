param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_EMITTED_LINEAGE_FULL_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$refLib      = Join-Path $RepoRoot "scripts\_lib_atlas_triad_boundary_v1.ps1"
$prepLib     = Join-Path $RepoRoot "scripts\_lib_atlas_triad_prep_boundary_v1.ps1"
$intentLib   = Join-Path $RepoRoot "scripts\_lib_atlas_triad_intents_v1.ps1"
$familyLib   = Join-Path $RepoRoot "scripts\_lib_atlas_triad_family_v1.ps1"
$emitLib     = Join-Path $RepoRoot "scripts\_lib_atlas_triad_emitted_lineage_v1.ps1"
$familyRun   = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_family_full_v1.ps1"

foreach($p in @($refLib,$prepLib,$intentLib,$familyLib,$emitLib,$familyRun)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){
    Die ("MISSING_REQUIRED_PATH: " + $p)
  }
}

. $refLib
. $prepLib
. $intentLib
. $familyLib
. $emitLib

$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source

$familyOut = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $familyRun -RepoRoot $RepoRoot 2>&1
foreach($x in @($familyOut)){ [Console]::Out.WriteLine([string]$x) }
if($LASTEXITCODE -ne 0){
  Die ("FAMILY_FULL_RUN_FAILED exit=" + $LASTEXITCODE)
}

$joined = (@($familyOut) -join "`n")
if($joined.IndexOf("ATLAS_TRIAD_FAMILY_FULL_OK",[StringComparison]::Ordinal) -lt 0){
  Die "FAMILY_FULL_RUN_TOKEN_MISSING"
}

$paths = Get-AtlasTriadEmittedLineagePaths $RepoRoot

$refObj         = Get-AtlasLastNdjsonObject $paths.TriadRefsPath
$restorePrepObj = Get-AtlasLastNdjsonObject $paths.RestorePrepPath
$updatePrepObj  = Get-AtlasLastNdjsonObject $paths.UpdatePrepPath
$restoreReqObj  = Get-AtlasLastNdjsonObject $paths.RestoreRequestPath
$artifactReqObj = Get-AtlasLastNdjsonObject $paths.ArtifactApplyRequestPath

$refClass         = Test-AtlasTriadFamilyObject $refObj
$restorePrepClass = Test-AtlasTriadFamilyObject $restorePrepObj
$updatePrepClass  = Test-AtlasTriadFamilyObject $updatePrepObj
$restoreReqClass  = Test-AtlasTriadFamilyObject $restoreReqObj
$artifactReqClass = Test-AtlasTriadFamilyObject $artifactReqObj

if($refClass -ne "reference"){ Die ("REFERENCE_CLASS_MISMATCH: " + $refClass) }
if($restorePrepClass -ne "restore-prep"){ Die ("RESTORE_PREP_CLASS_MISMATCH: " + $restorePrepClass) }
if($updatePrepClass -ne "update-prep"){ Die ("UPDATE_PREP_CLASS_MISMATCH: " + $updatePrepClass) }
if($restoreReqClass -ne "restore-request"){ Die ("RESTORE_REQUEST_CLASS_MISMATCH: " + $restoreReqClass) }
if($artifactReqClass -ne "artifact-apply-request"){ Die ("ARTIFACT_APPLY_CLASS_MISMATCH: " + $artifactReqClass) }

$runId = "atlas_triad_emitted_lineage_" + (Get-Date -Format "yyyyMMdd_HHmmss_fff") + "_" + $PID
$receiptPath = Add-AtlasTriadEmittedLineageReceipt `
  -RepoRoot $RepoRoot `
  -RunId $runId `
  -ReferenceClass $refClass `
  -RestorePrepClass $restorePrepClass `
  -UpdatePrepClass $updatePrepClass `
  -RestoreRequestClass $restoreReqClass `
  -ArtifactApplyClass $artifactReqClass

Ok ("REFERENCE_CLASS=" + $refClass)
Ok ("RESTORE_PREP_CLASS=" + $restorePrepClass)
Ok ("UPDATE_PREP_CLASS=" + $updatePrepClass)
Ok ("RESTORE_REQUEST_CLASS=" + $restoreReqClass)
Ok ("ARTIFACT_APPLY_CLASS=" + $artifactReqClass)
Ok ("RECEIPT_PATH=" + $receiptPath)
Write-Host "ATLAS_TRIAD_EMITTED_LINEAGE_FULL_OK" -ForegroundColor Green

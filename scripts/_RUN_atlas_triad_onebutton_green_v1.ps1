param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_ONEBUTTON_GREEN_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Ensure-Dir([string]$Path){
  if([string]::IsNullOrWhiteSpace($Path)){ Die "ENSURE_DIR_EMPTY" }
  if(-not (Test-Path -LiteralPath $Path -PathType Container)){
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
  }
}

function Write-Utf8NoBomLf([string]$Path,[string]$Text){
  $dir = Split-Path -Parent $Path
  if($dir){ Ensure-Dir $dir }
  $t = $Text.Replace("`r`n","`n").Replace("`r","`n")
  if(-not $t.EndsWith("`n")){ $t += "`n" }
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($Path,$t,$enc)
}

function Read-Utf8NoBom([string]$Path){
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::ReadAllText($Path,$enc)
}

function Sha256HexFile([string]$Path){
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $fs = [System.IO.File]::OpenRead($Path)
    try { $h = $sha.ComputeHash($fs) } finally { $fs.Dispose() }
  } finally {
    $sha.Dispose()
  }
  $sb = New-Object System.Text.StringBuilder
  foreach($b in $h){ [void]$sb.AppendFormat("{0:x2}", $b) }
  $sb.ToString()
}

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source

$RefSmoke      = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_boundary_smoke_v1.ps1"
$PrepSmoke     = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_prep_boundary_smoke_v1.ps1"
$IntentSmoke   = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_job_taxonomy_and_intents_smoke_v1.ps1"
$FamilyFull    = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_family_full_v1.ps1"
$EmittedFull   = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_emitted_lineage_full_v1.ps1"
$LineageRcpt   = Join-Path $RepoRoot "proofs\receipts\atlas_triad_emitted_lineage.ndjson"

foreach($p in @($RefSmoke,$PrepSmoke,$IntentSmoke,$FamilyFull,$EmittedFull)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_REQUIRED_RUNNER: " + $p) }
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$bundleRoot = Join-Path $RepoRoot ("proofs\receipts\atlas_triad_onebutton_green\" + $stamp)
Ensure-Dir $bundleRoot

$transcriptLines = New-Object System.Collections.Generic.List[string]

function Run-Step([string]$Label,[string]$Path,[string]$ExpectedToken){
  [void]$transcriptLines.Add("STEP_BEGIN=" + $Label)
  [void]$transcriptLines.Add("RUNNER=" + $Path)

  $out = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $Path -RepoRoot $RepoRoot 2>&1
  foreach($x in @($out)){
    $s = [string]$x
    [Console]::Out.WriteLine($s)
    [void]$transcriptLines.Add($s)
  }

  if($LASTEXITCODE -ne 0){
    Die ("STEP_FAILED: " + $Label + " exit=" + $LASTEXITCODE)
  }

  $joined = (@($out) -join "`n")
  if($joined.IndexOf($ExpectedToken,[StringComparison]::Ordinal) -lt 0){
    Die ("STEP_TOKEN_MISSING: " + $Label + " token=" + $ExpectedToken)
  }

  [void]$transcriptLines.Add("STEP_OK=" + $Label)
}

Run-Step "reference-boundary-smoke" $RefSmoke    "ATLAS_TRIAD_BOUNDARY_SMOKE_OK"
Run-Step "prep-boundary-smoke"      $PrepSmoke   "ATLAS_TRIAD_PREP_BOUNDARY_SMOKE_OK"
Run-Step "taxonomy-intents-smoke"   $IntentSmoke "ATLAS_TRIAD_JOB_TAXONOMY_AND_INTENTS_SMOKE_OK"
Run-Step "family-full"              $FamilyFull  "ATLAS_TRIAD_FAMILY_FULL_OK"
Run-Step "emitted-lineage-full"     $EmittedFull "ATLAS_TRIAD_EMITTED_LINEAGE_FULL_OK"

$transcriptPath = Join-Path $bundleRoot "transcript.txt"
$summaryPath    = Join-Path $bundleRoot "summary.txt"
$shaPath        = Join-Path $bundleRoot "sha256sums.txt"
$lineageCopy    = Join-Path $bundleRoot "atlas_triad_emitted_lineage.ndjson"

Write-Utf8NoBomLf $transcriptPath (($transcriptLines.ToArray()) -join "`n")

$summary = @(
  "ATLAS TRIAD ONEBUTTON GREEN V1",
  "BUNDLE_ROOT=" + $bundleRoot,
  "REFERENCE_BOUNDARY=OK",
  "PREP_BOUNDARY=OK",
  "JOB_TAXONOMY_AND_INTENTS=OK",
  "FAMILY_FULL=OK",
  "EMITTED_LINEAGE_FULL=OK",
  "STABLE_TOKEN=ATLAS_TRIAD_ONEBUTTON_GREEN_OK"
) -join "`n"
Write-Utf8NoBomLf $summaryPath $summary

if(Test-Path -LiteralPath $LineageRcpt -PathType Leaf){
  $lineageRaw = Read-Utf8NoBom $LineageRcpt
  Write-Utf8NoBomLf $lineageCopy $lineageRaw
} else {
  Die ("MISSING_LINEAGE_RECEIPT: " + $LineageRcpt)
}

$rows = New-Object System.Collections.Generic.List[string]
foreach($f in @(
  @{ Rel="transcript.txt"; Path=$transcriptPath },
  @{ Rel="summary.txt"; Path=$summaryPath },
  @{ Rel="atlas_triad_emitted_lineage.ndjson"; Path=$lineageCopy }
)){
  $hex = Sha256HexFile $f.Path
  [void]$rows.Add($hex + "  " + $f.Rel)
}
Write-Utf8NoBomLf $shaPath (($rows.ToArray()) -join "`n")

Ok ("BUNDLE_ROOT=" + $bundleRoot)
Ok ("TRANSCRIPT_PATH=" + $transcriptPath)
Ok ("SUMMARY_PATH=" + $summaryPath)
Ok ("SHA256SUMS_PATH=" + $shaPath)
Ok ("LINEAGE_RECEIPT_COPY=" + $lineageCopy)
Write-Host "ATLAS_TRIAD_ONEBUTTON_GREEN_OK" -ForegroundColor Green

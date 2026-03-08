param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TRIAD_BOUNDARY_SMOKE_FAIL: " + $m) }
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

$triadLib = Join-Path $RepoRoot "scripts\_lib_atlas_triad_boundary_v1.ps1"
$intake   = Join-Path $RepoRoot "scripts\_RUN_atlas_triad_reference_intake_v1.ps1"
$tvDir    = Join-Path $RepoRoot "test_vectors\atlas_triad_boundary"
$tvPath   = Join-Path $tvDir "minimal_reference.v1.json"

if(-not (Test-Path -LiteralPath $triadLib -PathType Leaf)){ Die ("MISSING_TRIAD_LIB: " + $triadLib) }
if(-not (Test-Path -LiteralPath $intake -PathType Leaf)){ Die ("MISSING_INTAKE_RUNNER: " + $intake) }

. $triadLib

if(-not (Test-Path -LiteralPath $tvDir -PathType Container)){
  New-Item -ItemType Directory -Force -Path $tvDir | Out-Null
}

$vectorObj = [ordered]@{
  schema             = "atlas.triad.reference.v1"
  triad_ref          = "triad://restore-set/minimal-v1"
  snapshot_ref       = "snapshot://device/dev-1/2026-03-08T00:00:00Z"
  source_packet_id   = "atlaspacket_minimal_reference_v1"
  source_content_ref = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
  device_id          = "dev-1"
  captured_utc       = "2026-03-08T00:00:00Z"
  handoff_kind       = "reference-intake"
  note               = "minimal triad boundary smoke vector"
}
$vectorJson = ($vectorObj | ConvertTo-Json -Compress -Depth 10)
Write-Utf8NoBomLf $tvPath $vectorJson
Ok ("WROTE_VECTOR=" + $tvPath)

$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source
$out = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $intake -RepoRoot $RepoRoot -ReferencePath $tvPath 2>&1
foreach($x in @($out)){ [Console]::Out.WriteLine([string]$x) }

if($LASTEXITCODE -ne 0){
  Die ("INTAKE_FAILED exit=" + $LASTEXITCODE)
}

$joined = (@($out) -join "`n")
if($joined.IndexOf("ATLAS_TRIAD_REFERENCE_INTAKE_OK",[StringComparison]::Ordinal) -lt 0){
  Die "INTAKE_TOKEN_MISSING"
}

$paths = Get-AtlasTriadBoundaryPaths $RepoRoot
if(-not (Test-Path -LiteralPath $paths.TriadRefsPath -PathType Leaf)){ Die ("TRIAD_REFS_PATH_MISSING: " + $paths.TriadRefsPath) }
if(-not (Test-Path -LiteralPath $paths.JobLedgerPath -PathType Leaf)){ Die ("JOB_LEDGER_PATH_MISSING: " + $paths.JobLedgerPath) }

$triadRefsRaw = Read-Utf8NoBom $paths.TriadRefsPath
$jobsRaw      = Read-Utf8NoBom $paths.JobLedgerPath

if($triadRefsRaw.IndexOf('"type":"atlas.triad.reference.intake.v1"',[StringComparison]::Ordinal) -lt 0){
  Die "TRIAD_REFS_APPEND_MISSING"
}
if($jobsRaw.IndexOf('"job_type":"triad-reference-intake"',[StringComparison]::Ordinal) -lt 0){
  Die "JOB_LEDGER_APPEND_MISSING"
}

Ok ("TRIAD_REFS_PATH=" + $paths.TriadRefsPath)
Ok ("JOB_LEDGER_PATH=" + $paths.JobLedgerPath)
Write-Host "ATLAS_TRIAD_BOUNDARY_SMOKE_OK" -ForegroundColor Green

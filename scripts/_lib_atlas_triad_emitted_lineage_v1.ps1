Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-AtlasTriadEmittedLineagePaths([string]$RepoRoot){
  if([string]::IsNullOrWhiteSpace($RepoRoot)){ throw "ATLAS_TRIAD_EMITTED_LINEAGE_EMPTY_REPOROOT" }
  $r = (Resolve-Path -LiteralPath $RepoRoot).Path
  [pscustomobject]@{
    RepoRoot                = $r
    TriadRefsPath           = (Join-Path $r "data\triad_references.ndjson")
    RestorePrepPath         = (Join-Path $r "data\triad_restore_prep.ndjson")
    UpdatePrepPath          = (Join-Path $r "data\triad_update_prep.ndjson")
    RestoreRequestPath      = (Join-Path $r "data\triad_restore_requests.ndjson")
    ArtifactApplyRequestPath= (Join-Path $r "data\triad_artifact_apply_requests.ndjson")
    JobLedgerPath           = (Join-Path $r "data\jobs.ndjson")
    ReceiptPath             = (Join-Path $r "proofs\receipts\atlas_triad_emitted_lineage.ndjson")
  }
}

function Read-Utf8NoBom([string]$Path){
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::ReadAllText($Path,$enc)
}

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

function Append-Utf8NoBomLfLine([string]$Path,[string]$Line){
  $dir = Split-Path -Parent $Path
  if($dir -and -not (Test-Path -LiteralPath $dir -PathType Container)){
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
  }
  if(Test-Path -LiteralPath $Path -PathType Leaf){
    $existing = Read-Utf8NoBom $Path
    $norm = $existing.Replace("`r`n","`n").Replace("`r","`n")
    if($norm.Length -gt 0 -and -not $norm.EndsWith("`n")){ $norm += "`n" }
    $norm += $Line + "`n"
    Write-Utf8NoBomLf $Path $norm
  }
  else {
    Write-Utf8NoBomLf $Path ($Line + "`n")
  }
}

function Get-AtlasLastNdjsonObject([string]$Path){
  if(-not (Test-Path -LiteralPath $Path -PathType Leaf)){ throw ("ATLAS_TRIAD_EMITTED_LINEAGE_MISSING_PATH: " + $Path) }
  $raw = Read-Utf8NoBom $Path
  $norm = $raw.Replace("`r`n","`n").Replace("`r","`n")
  $lines = @(@($norm.Split("`n")) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  if($lines.Count -lt 1){ throw ("ATLAS_TRIAD_EMITTED_LINEAGE_EMPTY_FILE: " + $Path) }
  $last = $lines[$lines.Count - 1]
  try {
    return ($last | ConvertFrom-Json -ErrorAction Stop)
  }
  catch {
    throw ("ATLAS_TRIAD_EMITTED_LINEAGE_JSON_PARSE_FAIL: " + $Path + " :: " + $_.Exception.Message)
  }
}

function Add-AtlasTriadEmittedLineageReceipt(
  [string]$RepoRoot,
  [string]$RunId,
  [string]$ReferenceClass,
  [string]$RestorePrepClass,
  [string]$UpdatePrepClass,
  [string]$RestoreRequestClass,
  [string]$ArtifactApplyClass
){
  $paths = Get-AtlasTriadEmittedLineagePaths $RepoRoot
  $obj = [ordered]@{
    type                    = "atlas.triad.emitted_lineage.receipt.v1"
    utc                     = [DateTime]::UtcNow.ToString("O")
    run_id                  = $RunId
    reference_class         = $ReferenceClass
    restore_prep_class      = $RestorePrepClass
    update_prep_class       = $UpdatePrepClass
    restore_request_class   = $RestoreRequestClass
    artifact_apply_class    = $ArtifactApplyClass
    triad_refs_path         = $paths.TriadRefsPath
    restore_prep_path       = $paths.RestorePrepPath
    update_prep_path        = $paths.UpdatePrepPath
    restore_request_path    = $paths.RestoreRequestPath
    artifact_apply_path     = $paths.ArtifactApplyRequestPath
    job_ledger_path         = $paths.JobLedgerPath
  }
  $json = ($obj | ConvertTo-Json -Compress -Depth 10)
  Append-Utf8NoBomLfLine -Path $paths.ReceiptPath -Line $json
  return $paths.ReceiptPath
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-AtlasTriadBoundaryPaths([string]$RepoRoot){
  if([string]::IsNullOrWhiteSpace($RepoRoot)){ throw "ATLAS_TRIAD_PATHS_EMPTY_REPOROOT" }
  $r = (Resolve-Path -LiteralPath $RepoRoot).Path
  [pscustomobject]@{
    RepoRoot            = $r
    DataDir             = (Join-Path $r "data")
    TriadRefsPath       = (Join-Path $r "data\triad_references.ndjson")
    JobLedgerPath       = (Join-Path $r "data\jobs.ndjson")
    HistoryLibPath      = (Join-Path $r "scripts\_lib_atlas_history_jobs_v1.ps1")
    SchemaPath          = (Join-Path $r "schemas\atlas.triad.reference.v1.json")
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

function Test-AtlasTriadReferenceV1([object]$Obj){
  if($null -eq $Obj){ throw "ATLAS_TRIAD_REF_NULL" }

  $required = @(
    "schema",
    "triad_ref",
    "snapshot_ref",
    "source_packet_id",
    "source_content_ref",
    "device_id",
    "captured_utc",
    "handoff_kind"
  )

  foreach($k in @($required)){
    if(-not ($Obj.PSObject.Properties.Name -contains $k)){
      throw ("ATLAS_TRIAD_REF_MISSING_FIELD: " + $k)
    }
    $v = [string]$Obj.$k
    if([string]::IsNullOrWhiteSpace($v)){
      throw ("ATLAS_TRIAD_REF_EMPTY_FIELD: " + $k)
    }
  }

  if([string]$Obj.schema -ne "atlas.triad.reference.v1"){
    throw ("ATLAS_TRIAD_REF_BAD_SCHEMA: " + [string]$Obj.schema)
  }

  $allowedKinds = @("reference-intake","restore-prep","update-prep")
  if($allowedKinds -notcontains ([string]$Obj.handoff_kind)){
    throw ("ATLAS_TRIAD_REF_BAD_HANDOFF_KIND: " + [string]$Obj.handoff_kind)
  }

  $true
}

function Add-AtlasTriadReferenceIntakeLine(
  [string]$RepoRoot,
  [string]$JobId,
  [object]$ReferenceObj
){
  $paths = Get-AtlasTriadBoundaryPaths $RepoRoot

  $line = [ordered]@{
    type               = "atlas.triad.reference.intake.v1"
    utc                = [DateTime]::UtcNow.ToString("O")
    job_id             = $JobId
    schema             = [string]$ReferenceObj.schema
    triad_ref          = [string]$ReferenceObj.triad_ref
    snapshot_ref       = [string]$ReferenceObj.snapshot_ref
    source_packet_id   = [string]$ReferenceObj.source_packet_id
    source_content_ref = [string]$ReferenceObj.source_content_ref
    device_id          = [string]$ReferenceObj.device_id
    captured_utc       = [string]$ReferenceObj.captured_utc
    handoff_kind       = [string]$ReferenceObj.handoff_kind
    note               = [string]$ReferenceObj.note
  }

  $json = ($line | ConvertTo-Json -Compress -Depth 10)
  Append-Utf8NoBomLfLine -Path $paths.TriadRefsPath -Line $json
  return $paths.TriadRefsPath
}

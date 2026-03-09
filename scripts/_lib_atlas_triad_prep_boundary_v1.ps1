Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-AtlasTriadPrepPaths([string]$RepoRoot){
  if([string]::IsNullOrWhiteSpace($RepoRoot)){ throw "ATLAS_TRIAD_PREP_PATHS_EMPTY_REPOROOT" }
  $r = (Resolve-Path -LiteralPath $RepoRoot).Path
  [pscustomobject]@{
    RepoRoot          = $r
    RestorePrepPath   = (Join-Path $r "data\triad_restore_prep.ndjson")
    UpdatePrepPath    = (Join-Path $r "data\triad_update_prep.ndjson")
    JobLedgerPath     = (Join-Path $r "data\jobs.ndjson")
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

function Test-AtlasTriadRestorePrepV1([object]$Obj){
  if($null -eq $Obj){ throw "ATLAS_TRIAD_RESTORE_PREP_NULL" }
  $required = @(
    "schema",
    "triad_ref",
    "snapshot_ref",
    "source_packet_id",
    "source_content_ref",
    "device_id",
    "captured_utc",
    "restore_target",
    "prep_reason"
  )
  foreach($k in @($required)){
    if(-not ($Obj.PSObject.Properties.Name -contains $k)){ throw ("ATLAS_TRIAD_RESTORE_PREP_MISSING_FIELD: " + $k) }
    $v = [string]$Obj.$k
    if([string]::IsNullOrWhiteSpace($v)){ throw ("ATLAS_TRIAD_RESTORE_PREP_EMPTY_FIELD: " + $k) }
  }
  if([string]$Obj.schema -ne "atlas.triad.restore_prep.v1"){
    throw ("ATLAS_TRIAD_RESTORE_PREP_BAD_SCHEMA: " + [string]$Obj.schema)
  }
  $true
}

function Test-AtlasTriadUpdatePrepV1([object]$Obj){
  if($null -eq $Obj){ throw "ATLAS_TRIAD_UPDATE_PREP_NULL" }
  $required = @(
    "schema",
    "triad_ref",
    "snapshot_ref",
    "source_packet_id",
    "source_content_ref",
    "device_id",
    "captured_utc",
    "artifact_target",
    "prep_reason"
  )
  foreach($k in @($required)){
    if(-not ($Obj.PSObject.Properties.Name -contains $k)){ throw ("ATLAS_TRIAD_UPDATE_PREP_MISSING_FIELD: " + $k) }
    $v = [string]$Obj.$k
    if([string]::IsNullOrWhiteSpace($v)){ throw ("ATLAS_TRIAD_UPDATE_PREP_EMPTY_FIELD: " + $k) }
  }
  if([string]$Obj.schema -ne "atlas.triad.update_prep.v1"){
    throw ("ATLAS_TRIAD_UPDATE_PREP_BAD_SCHEMA: " + [string]$Obj.schema)
  }
  $true
}

function Add-AtlasTriadRestorePrepLine([string]$RepoRoot,[string]$JobId,[object]$PrepObj){
  $paths = Get-AtlasTriadPrepPaths $RepoRoot
  $line = [ordered]@{
    type               = "atlas.triad.restore_prep.record.v1"
    utc                = [DateTime]::UtcNow.ToString("O")
    job_id             = $JobId
    schema             = [string]$PrepObj.schema
    triad_ref          = [string]$PrepObj.triad_ref
    snapshot_ref       = [string]$PrepObj.snapshot_ref
    source_packet_id   = [string]$PrepObj.source_packet_id
    source_content_ref = [string]$PrepObj.source_content_ref
    device_id          = [string]$PrepObj.device_id
    captured_utc       = [string]$PrepObj.captured_utc
    restore_target     = [string]$PrepObj.restore_target
    prep_reason        = [string]$PrepObj.prep_reason
    note               = [string]$PrepObj.note
  }
  $json = ($line | ConvertTo-Json -Compress -Depth 10)
  Append-Utf8NoBomLfLine -Path $paths.RestorePrepPath -Line $json
  return $paths.RestorePrepPath
}

function Add-AtlasTriadUpdatePrepLine([string]$RepoRoot,[string]$JobId,[object]$PrepObj){
  $paths = Get-AtlasTriadPrepPaths $RepoRoot
  $line = [ordered]@{
    type               = "atlas.triad.update_prep.record.v1"
    utc                = [DateTime]::UtcNow.ToString("O")
    job_id             = $JobId
    schema             = [string]$PrepObj.schema
    triad_ref          = [string]$PrepObj.triad_ref
    snapshot_ref       = [string]$PrepObj.snapshot_ref
    source_packet_id   = [string]$PrepObj.source_packet_id
    source_content_ref = [string]$PrepObj.source_content_ref
    device_id          = [string]$PrepObj.device_id
    captured_utc       = [string]$PrepObj.captured_utc
    artifact_target    = [string]$PrepObj.artifact_target
    prep_reason        = [string]$PrepObj.prep_reason
    note               = [string]$PrepObj.note
  }
  $json = ($line | ConvertTo-Json -Compress -Depth 10)
  Append-Utf8NoBomLfLine -Path $paths.UpdatePrepPath -Line $json
  return $paths.UpdatePrepPath
}

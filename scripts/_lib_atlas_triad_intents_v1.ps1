Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-AtlasTriadIntentPaths([string]$RepoRoot){
  if([string]::IsNullOrWhiteSpace($RepoRoot)){ throw "ATLAS_TRIAD_INTENT_PATHS_EMPTY_REPOROOT" }
  $r = (Resolve-Path -LiteralPath $RepoRoot).Path
  [pscustomobject]@{
    RepoRoot                 = $r
    RestoreRequestPath       = (Join-Path $r "data\triad_restore_requests.ndjson")
    ArtifactApplyRequestPath = (Join-Path $r "data\triad_artifact_apply_requests.ndjson")
    JobLedgerPath            = (Join-Path $r "data\jobs.ndjson")
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

function Add-AtlasTriadRestoreRequestLine([string]$RepoRoot,[string]$JobId,[object]$Obj){
  $paths = Get-AtlasTriadIntentPaths $RepoRoot
  $line = [ordered]@{
    type               = "atlas.triad.restore_request.record.v1"
    utc                = [DateTime]::UtcNow.ToString("O")
    job_id             = $JobId
    schema             = [string]$Obj.schema
    triad_ref          = [string]$Obj.triad_ref
    snapshot_ref       = [string]$Obj.snapshot_ref
    source_packet_id   = [string]$Obj.source_packet_id
    source_content_ref = [string]$Obj.source_content_ref
    device_id          = [string]$Obj.device_id
    captured_utc       = [string]$Obj.captured_utc
    restore_target     = [string]$Obj.restore_target
    request_reason     = [string]$Obj.request_reason
    note               = [string]$Obj.note
  }
  $json = ($line | ConvertTo-Json -Compress -Depth 10)
  Append-Utf8NoBomLfLine -Path $paths.RestoreRequestPath -Line $json
  return $paths.RestoreRequestPath
}

function Add-AtlasTriadArtifactApplyRequestLine([string]$RepoRoot,[string]$JobId,[object]$Obj){
  $paths = Get-AtlasTriadIntentPaths $RepoRoot
  $line = [ordered]@{
    type               = "atlas.triad.artifact_apply_request.record.v1"
    utc                = [DateTime]::UtcNow.ToString("O")
    job_id             = $JobId
    schema             = [string]$Obj.schema
    triad_ref          = [string]$Obj.triad_ref
    snapshot_ref       = [string]$Obj.snapshot_ref
    source_packet_id   = [string]$Obj.source_packet_id
    source_content_ref = [string]$Obj.source_content_ref
    device_id          = [string]$Obj.device_id
    captured_utc       = [string]$Obj.captured_utc
    artifact_target    = [string]$Obj.artifact_target
    request_reason     = [string]$Obj.request_reason
    note               = [string]$Obj.note
  }
  $json = ($line | ConvertTo-Json -Compress -Depth 10)
  Append-Utf8NoBomLfLine -Path $paths.ArtifactApplyRequestPath -Line $json
  return $paths.ArtifactApplyRequestPath
}

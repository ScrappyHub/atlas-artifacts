$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Ensure-Dir([string]$Path){
  if([string]::IsNullOrWhiteSpace($Path)){ throw "ENSURE_DIR_EMPTY" }
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

function Get-AtlasDataPaths([string]$RepoRoot){
  $dataDir = Join-Path $RepoRoot "data"
  Ensure-Dir $dataDir
  [pscustomobject]@{
    DataDir = $dataDir
    InventoryHistoryPath = Join-Path $dataDir "inventory_history.ndjson"
    JobLedgerPath        = Join-Path $dataDir "jobs.ndjson"
  }
}

function Append-NdjsonLine([string]$Path,[string]$JsonLine){
  if([string]::IsNullOrWhiteSpace($Path)){ throw "APPEND_NDJSON_PATH_EMPTY" }
  if([string]::IsNullOrWhiteSpace($JsonLine)){ throw "APPEND_NDJSON_LINE_EMPTY" }
  $dir = Split-Path -Parent $Path
  if($dir){ Ensure-Dir $dir }
  if(Test-Path -LiteralPath $Path -PathType Leaf){
    $existing = Read-Utf8NoBom $Path
    $norm = $existing.Replace("`r`n","`n").Replace("`r","`n")
    if($norm.Length -gt 0 -and -not $norm.EndsWith("`n")){ $norm += "`n" }
    $norm += $JsonLine + "`n"
    Write-Utf8NoBomLf $Path $norm
  } else {
    Write-Utf8NoBomLf $Path ($JsonLine + "`n")
  }
}

function New-AtlasJobId([string]$Prefix){
  if([string]::IsNullOrWhiteSpace($Prefix)){ $Prefix = "atlas" }
  $stamp = Get-Date -Format "yyyyMMdd_HHmmss_fff"
  ($Prefix + "_" + $stamp + "_" + $PID)
}

function Add-AtlasInventoryHistoryLine(
  [string]$RepoRoot,
  [string]$JobId,
  [string]$DeviceId,
  [string]$ContentRef,
  [string]$BlobPath,
  [string]$CapturedUtc,
  [string]$Source
){ 
  $paths = Get-AtlasDataPaths $RepoRoot
  $obj = [ordered]@{
    type         = "atlas.inventory.history.v1"
    utc          = [DateTime]::UtcNow.ToString("O")
    job_id       = $JobId
    device_id    = $DeviceId
    content_ref  = $ContentRef
    blob_path    = $BlobPath
    captured_utc = $CapturedUtc
    source       = $Source
  }
  $line = ($obj | ConvertTo-Json -Compress -Depth 10)
  Append-NdjsonLine $paths.InventoryHistoryPath $line
  $paths.InventoryHistoryPath
}

function Add-AtlasJobLedgerLine(
  [string]$RepoRoot,
  [string]$JobId,
  [string]$JobType,
  [string]$Status,
  [string]$DeviceId,
  [string]$ContentRef,
  [string]$PacketId,
  [string]$PacketDir,
  [string]$Note
){ 
  $paths = Get-AtlasDataPaths $RepoRoot
  $obj = [ordered]@{
    type        = "atlas.job.ledger.v1"
    utc         = [DateTime]::UtcNow.ToString("O")
    job_id      = $JobId
    job_type    = $JobType
    status      = $Status
    device_id   = $DeviceId
    content_ref = $ContentRef
    packet_id   = $PacketId
    packet_dir  = $PacketDir
    note        = $Note
  }
  $line = ($obj | ConvertTo-Json -Compress -Depth 10)
  Append-NdjsonLine $paths.JobLedgerPath $line
  $paths.JobLedgerPath
}

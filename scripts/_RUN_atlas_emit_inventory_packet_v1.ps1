param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [string]$DeviceId = "dev-1",
  [string]$HostName = "",
  [string]$OsFamily = "windows",
  [string]$OsVersion = "",
  [string]$Agent = "atlas-cli-dev",
  [string]$Strength = "evidence",
  [string[]]$Tags = @("lab")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_EMIT_PACKET_FAIL: " + $m) }

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

$historyLib = Join-Path $RepoRoot "scripts\_lib_atlas_history_jobs_v1.ps1"
if(-not (Test-Path -LiteralPath $historyLib -PathType Leaf)){ Die ("MISSING_HISTORY_LIB: " + $historyLib) }
. $historyLib

if([string]::IsNullOrWhiteSpace($HostName)){ $HostName = $env:COMPUTERNAME }
if([string]::IsNullOrWhiteSpace($OsVersion)){ $OsVersion = [Environment]::OSVersion.Version.ToString() }

$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$cliProj = Join-Path $RepoRoot "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj"
if(-not (Test-Path -LiteralPath $cliProj -PathType Leaf)){ Die ("MISSING_CLI_PROJ: " + $cliProj) }

$jobId = New-AtlasJobId "atlas_emit_packet"

$tagArgs = New-Object System.Collections.Generic.List[string]
foreach($t in @($Tags)){
  if(-not [string]::IsNullOrWhiteSpace($t)){
    [void]$tagArgs.Add("--tag")
    [void]$tagArgs.Add($t)
  }
}

Push-Location -LiteralPath $RepoRoot
try {
  $emitArgs = New-Object System.Collections.Generic.List[string]
  [void]$emitArgs.Add("run")
  [void]$emitArgs.Add("--project")
  [void]$emitArgs.Add($cliProj)
  [void]$emitArgs.Add("--")
  [void]$emitArgs.Add("emit-inventory")
  [void]$emitArgs.Add("--device")
  [void]$emitArgs.Add($DeviceId)
  [void]$emitArgs.Add("--hostname")
  [void]$emitArgs.Add($HostName)
  [void]$emitArgs.Add("--os-family")
  [void]$emitArgs.Add($OsFamily)
  [void]$emitArgs.Add("--os-version")
  [void]$emitArgs.Add($OsVersion)
  [void]$emitArgs.Add("--agent")
  [void]$emitArgs.Add($Agent)
  [void]$emitArgs.Add("--strength")
  [void]$emitArgs.Add($Strength)
  foreach($a in @($tagArgs)){ [void]$emitArgs.Add($a) }

  $emitOut = & $dotnet @($emitArgs.ToArray()) 2>&1
} finally {
  Pop-Location
}

foreach($x in @($emitOut)){ [Console]::Out.WriteLine($x) }
if($LASTEXITCODE -ne 0){ Die ("EMIT_INVENTORY_FAILED exit=" + $LASTEXITCODE) }

$contentRef    = $null
$blobPath      = $null
$capturedUtc   = $null
$emittedDevice = $null

foreach($x in @($emitOut)){
  if($x -is [string]){
    if($x.StartsWith("CONTENT_REF=")){ $contentRef = $x.Substring("CONTENT_REF=".Length).Trim() }
    elseif($x.StartsWith("BLOB_PATH=")){ $blobPath = $x.Substring("BLOB_PATH=".Length).Trim() }
    elseif($x.StartsWith("CAPTURED_UTC=")){ $capturedUtc = $x.Substring("CAPTURED_UTC=".Length).Trim() }
    elseif($x.StartsWith("DEVICE_ID=")){ $emittedDevice = $x.Substring("DEVICE_ID=".Length).Trim() }
  }
}

if([string]::IsNullOrWhiteSpace($contentRef)){ Die "MISSING_CONTENT_REF_LINE" }
if([string]::IsNullOrWhiteSpace($blobPath)){ Die "MISSING_BLOB_PATH_LINE" }
if([string]::IsNullOrWhiteSpace($capturedUtc)){ Die "MISSING_CAPTURED_UTC_LINE" }
if([string]::IsNullOrWhiteSpace($emittedDevice)){ Die "MISSING_DEVICE_ID_LINE" }
if(-not (Test-Path -LiteralPath $blobPath -PathType Leaf)){ Die ("BLOB_MISSING: " + $blobPath) }

$blobHex = Sha256HexFile $blobPath
if($contentRef -ne ("sha256:" + $blobHex)){ Die ("CONTENT_REF_BLOB_HASH_MISMATCH ref=" + $contentRef + " blob=" + $blobHex) }

$outboxRoot = Join-Path $RepoRoot "data\outbox"
Ensure-Dir $outboxRoot

$stageRoot = Join-Path $outboxRoot ("_stage_" + (Get-Date -Format "yyyyMMdd_HHmmss_fff") + "_" + $PID)
$payloadDir = Join-Path $stageRoot "payload"
Ensure-Dir $payloadDir

$contentRefPath = Join-Path $payloadDir "content_ref.txt"
$metaPath       = Join-Path $payloadDir "inventory.snapshot.meta.json"
$manifestPath   = Join-Path $stageRoot "manifest.json"
$packetIdPath   = Join-Path $stageRoot "packet_id.txt"
$shaPath        = Join-Path $stageRoot "sha256sums.txt"
$pledgePath     = Join-Path $RepoRoot "data\pledges.ndjson"

Write-Utf8NoBomLf $contentRefPath $contentRef

$metaObj = [ordered]@{
  schema       = "atlas.inventory.packet.meta.v1"
  device_id    = $emittedDevice
  captured_utc = $capturedUtc
  content_ref  = $contentRef
  blob_sha256  = $blobHex
  blob_path    = $blobPath
}
$metaJson = ($metaObj | ConvertTo-Json -Compress -Depth 10)
Write-Utf8NoBomLf $metaPath $metaJson

$manifestObj = [ordered]@{
  schema        = "atlas.packet.manifest.v1"
  packet_type   = "atlas.inventory.snapshot.packet.v1"
  device_id     = $emittedDevice
  captured_utc  = $capturedUtc
  content_ref   = $contentRef
  payload_files = @(
    "payload/content_ref.txt",
    "payload/inventory.snapshot.meta.json"
  )
}
$manifestJson = ($manifestObj | ConvertTo-Json -Compress -Depth 10)
Write-Utf8NoBomLf $manifestPath $manifestJson

$packetId = Sha256HexFile $manifestPath
Write-Utf8NoBomLf $packetIdPath $packetId

$rows = New-Object System.Collections.Generic.List[string]
$filesForSums = @(
  @{ Rel = "payload/content_ref.txt"; Path = $contentRefPath },
  @{ Rel = "payload/inventory.snapshot.meta.json"; Path = $metaPath },
  @{ Rel = "manifest.json"; Path = $manifestPath },
  @{ Rel = "packet_id.txt"; Path = $packetIdPath }
)

foreach($f in @($filesForSums)){
  $hex = Sha256HexFile $f.Path
  [void]$rows.Add($hex + "  " + $f.Rel)
}

$preSelfText = (($rows.ToArray()) -join "`n")
Write-Utf8NoBomLf $shaPath $preSelfText
$shaSelf = Sha256HexFile $shaPath
[void]$rows.Add($shaSelf + "  sha256sums.txt")
Write-Utf8NoBomLf $shaPath (($rows.ToArray()) -join "`n")

$packetDir = Join-Path $outboxRoot $packetId
if(Test-Path -LiteralPath $packetDir){
  Remove-Item -LiteralPath $stageRoot -Recurse -Force
} else {
  Move-Item -LiteralPath $stageRoot -Destination $packetDir
}

$pledgeObj = [ordered]@{
  type        = "atlas.inventory.packet.pledge.v1"
  utc         = [DateTime]::UtcNow.ToString("O")
  device_id   = $emittedDevice
  content_ref = $contentRef
  packet_id   = $packetId
  packet_dir  = $packetDir
}
$pledgeLine = ($pledgeObj | ConvertTo-Json -Compress -Depth 10)

$pledgeDir = Split-Path -Parent $pledgePath
Ensure-Dir $pledgeDir
if(Test-Path -LiteralPath $pledgePath -PathType Leaf){
  $existing = [System.IO.File]::ReadAllText($pledgePath, (New-Object System.Text.UTF8Encoding($false)))
  $append = $existing.Replace("`r`n","`n").Replace("`r","`n")
  if($append.Length -gt 0 -and -not $append.EndsWith("`n")){ $append += "`n" }
  $append += $pledgeLine + "`n"
  Write-Utf8NoBomLf $pledgePath $append
} else {
  Write-Utf8NoBomLf $pledgePath ($pledgeLine + "`n")
}

$null = Add-AtlasInventoryHistoryLine -RepoRoot $RepoRoot -JobId $jobId -DeviceId $emittedDevice -ContentRef $contentRef -BlobPath $blobPath -CapturedUtc $capturedUtc -Source "atlas.inventory.snapshot.packet.emit.v1"
$null = Add-AtlasJobLedgerLine -RepoRoot $RepoRoot -JobId $jobId -JobType "emit-inventory-packet" -Status "ok" -DeviceId $emittedDevice -ContentRef $contentRef -PacketId $packetId -PacketDir $packetDir -Note "emit packet completed"

Write-Host "EMIT_INVENTORY_PACKET_OK" -ForegroundColor Green
Write-Host ("JOB_ID=" + $jobId)
Write-Host ("CONTENT_REF=" + $contentRef)
Write-Host ("BLOB_PATH=" + $blobPath)
Write-Host ("MANIFEST_SHA256=" + (Sha256HexFile (Join-Path $packetDir "manifest.json")))
Write-Host ("PACKET_ID_FILE_SHA256=" + (Sha256HexFile (Join-Path $packetDir "packet_id.txt")))
Write-Host ("PACKET_ID=" + $packetId)
Write-Host ("PACKET_DIR=" + $packetDir)
Write-Host ("PLEDGE_PATH=" + $pledgePath)

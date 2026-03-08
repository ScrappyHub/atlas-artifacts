param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TIER0_FULL_SELFTEST_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Invoke-ChildPwshCapture(
  [string]$PSExe,
  [string]$ScriptPath,
  [string]$RepoRoot,
  [string]$PacketDir
){
  $lines = New-Object System.Collections.Generic.List[string]
  $exitCode = 0

  try {
    if([string]::IsNullOrWhiteSpace($PacketDir)){
      $out = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ScriptPath -RepoRoot $RepoRoot 2>&1
    } else {
      $out = & $PSExe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ScriptPath -RepoRoot $RepoRoot -PacketDir $PacketDir 2>&1
    }

    foreach($x in @($out)){
      if($null -eq $x){ continue }
      $s = [string]$x
      [void]$lines.Add($s)
      [Console]::Out.WriteLine($s)
    }
    $exitCode = $LASTEXITCODE
  }
  catch {
    $msg = $_.ToString()
    if(-not [string]::IsNullOrWhiteSpace($msg)){
      $norm = $msg.Replace("`r`n","`n").Replace("`r","`n")
      foreach($line in @($norm.Split("`n"))){
        if([string]::IsNullOrWhiteSpace($line)){ continue }
        [void]$lines.Add($line)
        [Console]::Out.WriteLine($line)
      }
    }
    if($LASTEXITCODE -ne $null){
      $exitCode = $LASTEXITCODE
    } else {
      $exitCode = 1
    }
  }

  [pscustomobject]@{
    ExitCode = [int]$exitCode
    Lines    = @($lines.ToArray())
    Joined   = (@($lines.ToArray()) -join "`n")
  }
}

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){
  Die ("MISSING_REPOROOT: " + $RepoRoot)
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$PSExe = (Get-Command powershell.exe -ErrorAction Stop).Source
$Dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source

$SharedProj = Join-Path $RepoRoot "src\shared\Atlas.Handoff\Atlas.Handoff.csproj"
$CliProj    = Join-Path $RepoRoot "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj"

$RunBlobSmoke  = Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_smoke_emit_and_verify_blob_v1.ps1"
$RunEmitPacket = Join-Path $RepoRoot "scripts\_RUN_atlas_emit_inventory_packet_v1.ps1"
$RunVerifyPkt  = Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_packet_verify_v1.ps1"

foreach($p in @($SharedProj,$CliProj,$RunBlobSmoke,$RunEmitPacket,$RunVerifyPkt)){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){
    Die ("MISSING_REQUIRED_PATH: " + $p)
  }
}

Push-Location -LiteralPath $RepoRoot
try {
  & $Dotnet build $SharedProj | Out-Host
  if($LASTEXITCODE -ne 0){ Die ("DOTNET_BUILD_SHARED_FAILED exit=" + $LASTEXITCODE) }
  Ok "BUILD_SHARED_OK"

  & $Dotnet build $CliProj | Out-Host
  if($LASTEXITCODE -ne 0){ Die ("DOTNET_BUILD_CLI_FAILED exit=" + $LASTEXITCODE) }
  Ok "BUILD_CLI_OK"
}
finally {
  Pop-Location
}

$blobRes = Invoke-ChildPwshCapture -PSExe $PSExe -ScriptPath $RunBlobSmoke -RepoRoot $RepoRoot -PacketDir ""
if($blobRes.ExitCode -ne 0){
  Die ("RUN_EMIT_VERIFY_BLOB_FAILED exit=" + $blobRes.ExitCode)
}
if($blobRes.Joined.IndexOf("ATLAS_TIER0_SMOKE_EMIT_AND_VERIFY_BLOB_OK",[StringComparison]::Ordinal) -lt 0){
  Die "RUN_EMIT_VERIFY_BLOB_OK_TOKEN_MISSING"
}
Ok "RUN_EMIT_VERIFY_BLOB_OK"

$emitRes = Invoke-ChildPwshCapture -PSExe $PSExe -ScriptPath $RunEmitPacket -RepoRoot $RepoRoot -PacketDir ""
if($emitRes.ExitCode -ne 0){
  Die ("RUN_EMIT_PACKET_FAILED exit=" + $emitRes.ExitCode)
}
if($emitRes.Joined.IndexOf("EMIT_INVENTORY_PACKET_OK",[StringComparison]::Ordinal) -lt 0){
  Die "RUN_EMIT_PACKET_OK_TOKEN_MISSING"
}

$packetDir = $null
foreach($x in @($emitRes.Lines)){
  if($x.StartsWith("PACKET_DIR=")){
    $packetDir = $x.Substring("PACKET_DIR=".Length).Trim()
  }
}
if([string]::IsNullOrWhiteSpace($packetDir)){
  Die "RUN_EMIT_PACKET_MISSING_PACKET_DIR"
}
if(-not (Test-Path -LiteralPath $packetDir -PathType Container)){
  Die ("RUN_EMIT_PACKET_DIR_NOT_FOUND: " + $packetDir)
}
Ok "RUN_EMIT_PACKET_OK"
Ok ("RUN_EMIT_PACKET_DIR=" + $packetDir)

$verifyRes = Invoke-ChildPwshCapture -PSExe $PSExe -ScriptPath $RunVerifyPkt -RepoRoot $RepoRoot -PacketDir $packetDir
if($verifyRes.ExitCode -ne 0){
  Die ("RUN_VERIFY_PACKET_FAILED exit=" + $verifyRes.ExitCode)
}
if($verifyRes.Joined.IndexOf("ATLAS_TIER0_PACKET_VERIFY_OK",[StringComparison]::Ordinal) -lt 0){
  Die "RUN_VERIFY_PACKET_OK_TOKEN_MISSING"
}
Ok "RUN_VERIFY_PACKET_OK"

Write-Host "ATLAS_TIER0_FULL_SELFTEST_OK" -ForegroundColor Green

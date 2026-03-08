param([Parameter(Mandatory=$true)][string]$RepoRoot)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){
  throw ("ATLAS_TIER0_NEGATIVE_VECTORS_FAIL: " + $m)
}

function Ok([string]$m){
  Write-Host ("OK: " + $m) -ForegroundColor Green
}

function Ensure-Dir([string]$Path){
  if([string]::IsNullOrWhiteSpace($Path)){
    Die "ENSURE_DIR_EMPTY"
  }
  if(-not (Test-Path -LiteralPath $Path -PathType Container)){
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
  }
}

function Write-Utf8NoBomLf([string]$Path,[string]$Text){
  $dir = Split-Path -Parent $Path
  if($dir){
    Ensure-Dir $dir
  }
  $t = $Text.Replace("`r`n","`n").Replace("`r","`n")
  if(-not $t.EndsWith("`n")){
    $t += "`n"
  }
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($Path,$t,$enc)
}

function Invoke-ChildPwshCapture([string]$PSExe,[string]$ScriptPath,[string]$RepoRoot,[string]$PacketDir){
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

  return [pscustomobject]@{
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
$EmitPacketRun = Join-Path $RepoRoot "scripts\_RUN_atlas_emit_inventory_packet_v1.ps1"
$VerifyPktRun  = Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_packet_verify_v1.ps1"

if(-not (Test-Path -LiteralPath $EmitPacketRun -PathType Leaf)){
  Die ("MISSING_RUNNER: " + $EmitPacketRun)
}
if(-not (Test-Path -LiteralPath $VerifyPktRun -PathType Leaf)){
  Die ("MISSING_RUNNER: " + $VerifyPktRun)
}

$emitRes = Invoke-ChildPwshCapture -PSExe $PSExe -ScriptPath $EmitPacketRun -RepoRoot $RepoRoot -PacketDir ""
if($emitRes.ExitCode -ne 0){
  Die ("EMIT_PACKET_FAILED exit=" + $emitRes.ExitCode)
}

$packetDir = $null
$baseContentRef = $null

foreach($x in @($emitRes.Lines)){
  if($x.StartsWith("PACKET_DIR=")){
    $packetDir = $x.Substring("PACKET_DIR=".Length).Trim()
  }
  elseif($x.StartsWith("CONTENT_REF=") -and [string]::IsNullOrWhiteSpace($baseContentRef)){
    $baseContentRef = $x.Substring("CONTENT_REF=".Length).Trim()
  }
}

if([string]::IsNullOrWhiteSpace($packetDir)){
  Die "MISSING_PACKET_DIR_LINE"
}
if(-not (Test-Path -LiteralPath $packetDir -PathType Container)){
  Die ("PACKET_DIR_MISSING: " + $packetDir)
}
Ok ("BASE_PACKET_DIR=" + $packetDir)

$vectorsRoot = Join-Path $RepoRoot "test_vectors\atlas_tier0_negative"
Ensure-Dir $vectorsRoot

$stamp = Get-Date -Format "yyyyMMdd_HHmmss_fff"

$cases = @(
  @{ Name = "missing_blob";        Token = "MISSING_BLOB" },
  @{ Name = "tampered_manifest";   Token = "PACKET_ID_TXT_MISMATCH" },
  @{ Name = "tampered_packet_id";  Token = "PACKET_ID_TXT_MISMATCH" },
  @{ Name = "tampered_sha256sums"; Token = "SHA256SUMS_HASH_MISMATCH" }
)

foreach($case in @($cases)){
  $dst = Join-Path $vectorsRoot ($stamp + "_" + $case.Name)
  if(Test-Path -LiteralPath $dst){
    Remove-Item -LiteralPath $dst -Recurse -Force
  }
  Copy-Item -LiteralPath $packetDir -Destination $dst -Recurse -Force

  $manifestPath = Join-Path $dst "manifest.json"
  $packetIdPath = Join-Path $dst "packet_id.txt"
  $shaPath      = Join-Path $dst "sha256sums.txt"

  if($case.Name -eq "missing_blob"){
    $manifestRaw = [System.IO.File]::ReadAllText($manifestPath, (New-Object System.Text.UTF8Encoding($false)))
    $m = [regex]::Match($manifestRaw, '"content_ref":"sha256:(?<hex>[0-9a-f]{64})"')
    if(-not $m.Success){
      Die ("MISSING_CONTENT_REF_IN_MANIFEST: " + $manifestPath)
    }
    $blobHex = $m.Groups["hex"].Value
    $blobPath = Join-Path $RepoRoot ("data\blobs\" + $blobHex)
    $blobBak  = $blobPath + ".bak_negative"

    if(Test-Path -LiteralPath $blobBak -PathType Leaf){
      Remove-Item -LiteralPath $blobBak -Force
    }
    if(Test-Path -LiteralPath $blobPath -PathType Leaf){
      Move-Item -LiteralPath $blobPath -Destination $blobBak -Force
    }
  }

  if($case.Name -eq "tampered_manifest"){
    $raw = [System.IO.File]::ReadAllText($manifestPath, (New-Object System.Text.UTF8Encoding($false)))
    if($raw.IndexOf('"packet_type":"atlas.inventory.snapshot.packet.v1"',[StringComparison]::Ordinal) -ge 0){
      $raw = $raw.Replace('"packet_type":"atlas.inventory.snapshot.packet.v1"','"packet_type":"atlas.inventory.snapshot.packet.v1.tampered"')
    } else {
      $raw = $raw + " "
    }
    Write-Utf8NoBomLf $manifestPath $raw
  }

  if($case.Name -eq "tampered_packet_id"){
    Write-Utf8NoBomLf $packetIdPath ("0" * 64)
  }

  if($case.Name -eq "tampered_sha256sums"){
    $raw = [System.IO.File]::ReadAllText($shaPath, (New-Object System.Text.UTF8Encoding($false)))
    $norm = $raw.Replace("`r`n","`n").Replace("`r","`n")
    $lines = @($norm.Split("`n"))
    $outLines = New-Object System.Collections.Generic.List[string]
    $didTamper = $false

    foreach($line in @($lines)){
      if([string]::IsNullOrWhiteSpace($line)){ continue }

      if(-not $didTamper -and $line -match '^(?<hex>[0-9a-f]{64})  (?<rel>.+)$'){
        $hex = $Matches["hex"]
        $rel = $Matches["rel"]

        if($rel -ne "sha256sums.txt"){
          $first = $hex.Substring(0,1)
          $swap = "0"
          if($first -eq "0"){ $swap = "1" }
          $badHex = $swap + $hex.Substring(1)
          [void]$outLines.Add($badHex + "  " + $rel)
          $didTamper = $true
          continue
        }
      }

      [void]$outLines.Add($line)
    }

    if(-not $didTamper){
      Die "TAMPER_SHA256SUMS_TARGET_NOT_FOUND"
    }

    Write-Utf8NoBomLf $shaPath (($outLines.ToArray()) -join "`n")
  }

  $verifyRes = Invoke-ChildPwshCapture -PSExe $PSExe -ScriptPath $VerifyPktRun -RepoRoot $RepoRoot -PacketDir $dst

  if($case.Name -eq "missing_blob"){
    $manifestRaw = [System.IO.File]::ReadAllText($manifestPath, (New-Object System.Text.UTF8Encoding($false)))
    $m = [regex]::Match($manifestRaw, '"content_ref":"sha256:(?<hex>[0-9a-f]{64})"')
    if($m.Success){
      $blobHex = $m.Groups["hex"].Value
      $blobPath = Join-Path $RepoRoot ("data\blobs\" + $blobHex)
      $blobBak  = $blobPath + ".bak_negative"
      if(Test-Path -LiteralPath $blobBak -PathType Leaf){
        Move-Item -LiteralPath $blobBak -Destination $blobPath -Force
      }
    }
  }

  if($verifyRes.ExitCode -eq 0){
    Die ("NEGATIVE_VECTOR_UNEXPECTED_SUCCESS: " + $case.Name)
  }

  if($verifyRes.Joined -notmatch [regex]::Escape($case.Token)){
    Die ("NEGATIVE_VECTOR_MISSING_TOKEN: " + $case.Name + " expected=" + $case.Token)
  }

  Ok ("NEGATIVE_VECTOR_OK: " + $case.Name + " token=" + $case.Token)
}

Write-Host "ATLAS_TIER0_NEGATIVE_VECTORS_OK" -ForegroundColor Green

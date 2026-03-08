param([Parameter(Mandatory=$true)][string]$RepoRoot)
$ErrorActionPreference="Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_SMOKE_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$cliProj = Join-Path $RepoRoot "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj"
if(-not (Test-Path -LiteralPath $cliProj -PathType Leaf)){ Die ("MISSING_CLI_PROJ: " + $cliProj) }

$deviceId  = "dev-1"
$hostname  = $env:COMPUTERNAME
$osFamily  = "windows"
$osVersion = ([Environment]::OSVersion.Version.ToString())

Push-Location -LiteralPath $RepoRoot
try {
  $out = & $dotnet run --project $cliProj -- emit-inventory --device $deviceId --hostname $hostname --os-family $osFamily --os-version $osVersion --agent "atlas-cli-dev" --strength "evidence" --tag "lab" 2>&1
} finally {
  Pop-Location
}

foreach($x in @($out)){ [Console]::Out.WriteLine($x) }
if($LASTEXITCODE -ne 0){ Die ("DOTNET_RUN_FAILED exit=" + $LASTEXITCODE) }

$blobPath = $null
foreach($x in @($out)){
  if($x -is [string] -and $x.StartsWith("BLOB_PATH=")){ $blobPath = $x.Substring("BLOB_PATH=".Length).Trim() }
}
if([string]::IsNullOrWhiteSpace($blobPath)){ Die "MISSING_BLOB_PATH_LINE" }
if(-not (Test-Path -LiteralPath $blobPath -PathType Leaf)){ Die ("BLOB_FILE_MISSING: " + $blobPath) }
$fi = Get-Item -LiteralPath $blobPath -ErrorAction Stop
Ok ("BLOB_EXISTS: " + $fi.FullName)
Ok ("BLOB_LEN: " + $fi.Length)
Write-Host "ATLAS_EMIT_INVENTORY_SMOKE_OK" -ForegroundColor Green

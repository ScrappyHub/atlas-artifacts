param([Parameter(Mandatory=$true)][string]$RepoRoot)
$ErrorActionPreference="Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_TIER0_SMOKE_FAIL: " + $m) }
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
  $emit = & $dotnet run --project $cliProj -- emit-inventory --device $deviceId --hostname $hostname --os-family $osFamily --os-version $osVersion --agent "atlas-cli-dev" --strength "evidence" --tag "lab" 2>&1
} finally { Pop-Location }
foreach($x in @($emit)){ [Console]::Out.WriteLine($x) }
if($LASTEXITCODE -ne 0){ Die ("EMIT_FAILED exit=" + $LASTEXITCODE) }

$ref = $null
foreach($x in @($emit)){ if($x -is [string] -and $x.StartsWith("CONTENT_REF=")){ $ref = $x.Substring("CONTENT_REF=".Length).Trim() } }
if([string]::IsNullOrWhiteSpace($ref)){ Die "MISSING_CONTENT_REF_LINE" }
Ok ("CAPTURED_REF=" + $ref)

Push-Location -LiteralPath $RepoRoot
try {
  $ver = & $dotnet run --project $cliProj -- verify-blob --ref $ref 2>&1
} finally { Pop-Location }
foreach($x in @($ver)){ [Console]::Out.WriteLine($x) }
if($LASTEXITCODE -ne 0){ Die ("VERIFY_BLOB_FAILED exit=" + $LASTEXITCODE) }

$ok = $false
foreach($x in @($ver)){ if($x -is [string] -and $x.Trim() -eq "VERIFY_BLOB_OK"){ $ok = $true } }
if(-not $ok){ Die "VERIFY_BLOB_OK_TOKEN_MISSING" }

Write-Host "ATLAS_TIER0_SMOKE_EMIT_AND_VERIFY_BLOB_OK" -ForegroundColor Green

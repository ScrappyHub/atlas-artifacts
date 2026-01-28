[CmdletBinding()]
param(
  [int]$Port = 5000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$atlas = Join-Path $PSScriptRoot "atlas.ps1"
$sln = Join-Path $repoRoot "atlas-update.sln"

Write-Host "== Atlas dev bootstrap =="

# Stop if running (safe)
try {
  powershell -NoProfile -ExecutionPolicy Bypass -File $atlas stop -Port $Port | Out-Host
} catch {
  Write-Host "(stop) ignored: $($_.Exception.Message)"
}

# Restore + build (FAIL FAST)
dotnet restore $sln | Out-Host
dotnet build $sln | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed; not starting Atlas." }

# Start background
powershell -NoProfile -ExecutionPolicy Bypass -File $atlas start -Port $Port | Out-Host

# Verify service is actually up before declaring success
$test = powershell -NoProfile -ExecutionPolicy Bypass -File $atlas test -Port $Port
$test | Format-List | Out-Host

Write-Host "`n== OK =="

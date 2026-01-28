[CmdletBinding()]
param(
  [Parameter(Position=0)]
  [ValidateSet("start","stop","status","test","restart")]
  [string]$Command = "status",

  [int]$Port = 5000,

  [string]$Project = "C:\dev\atlas-update\src\activation\Atlas.ActivationAuthority\Atlas.ActivationAuthority.csproj",

  [string]$Profile = "Atlas.Dev"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ListenerPid {
  param([int]$P)
  $line = (netstat -ano | Select-String -Pattern "^\s*TCP\s+127\.0\.0\.1:$P\s+.*\s+LISTENING\s+\d+\s*$" | Select-Object -First 1)
  if (-not $line) { return $null }

  $parts = ($line.Line -split "\s+") | Where-Object { $_ -ne "" }
  # last token is PID
  $pidText = $parts[-1]
  if ([int]::TryParse($pidText, [ref]$null)) { return [int]$pidText }
  return $null
}

function Stop-Listener {
  param([int]$P)

  $listenPid = Get-ListenerPid -P $P
  if (-not $listenPid) {
    Write-Host "Atlas not running on port $P"
    return
  }

  Write-Host "Stopping Atlas listener on port $P => PID $listenPid"
  & taskkill /PID $listenPid /T /F | Out-Null
}

function Start-Listener {
  param([int]$P)

  $listenPid = Get-ListenerPid -P $P
  if ($listenPid) {
    Write-Host "Atlas already running on port $P (PID $listenPid)"
    return
  }

  Write-Host "Starting Atlas on port $P in background (profile $Profile)..."

  $args = @(
    "run",
    "--project", $Project,
    "--launch-profile", $Profile
  )

  # Start detached dotnet run
  $proc = Start-Process -FilePath "dotnet" -ArgumentList $args -PassThru -WindowStyle Hidden

  # Wait for Kestrel to bind
  $tries = 0
  do {
    Start-Sleep -Milliseconds 250
    $tries++
    $listenPid = Get-ListenerPid -P $P
  } while (-not $listenPid -and $tries -lt 40)

  if (-not $listenPid) {
    Write-Host "dotnet started (PID $($proc.Id)) but port $P not listening yet."
    Write-Host "Run foreground for logs:"
    Write-Host "  dotnet run --project `"$Project`" --launch-profile `"$Profile`""
    return
  }

  Write-Host "Atlas started => port $P PID $listenPid (Atlas.ActivationAuthority)"
}

function Status-Listener {
  param([int]$P)
  $listenPid = Get-ListenerPid -P $P
  if ($listenPid) {
    Write-Host "Atlas listening on port $P => PID $listenPid (Atlas.ActivationAuthority)"
  } else {
    Write-Host "Atlas not running on port $P"
  }
}

function Test-Listener {
  param([int]$P)

  $base = "http://127.0.0.1:$P"
  $root = Invoke-RestMethod -Method Get -Uri "$base/" -TimeoutSec 3
  $health = Invoke-RestMethod -Method Get -Uri "$base/health" -TimeoutSec 3

  [pscustomobject]@{
    ok     = $true
    root   = $root
    health = $health
  }
}

switch ($Command) {
  "start"   { Start-Listener -P $Port }
  "stop"    { Stop-Listener -P $Port }
  "restart" { Stop-Listener -P $Port; Start-Listener -P $Port }
  "status"  { Status-Listener -P $Port }
  "test"    { Test-Listener -P $Port }
}

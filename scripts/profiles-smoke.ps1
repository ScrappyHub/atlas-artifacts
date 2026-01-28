[CmdletBinding()]
param(
  [string]$BaseUrl  = "http://127.0.0.1:5000",
  [string]$TenantId = "t_dev",
  [string]$DeviceId = $env:COMPUTERNAME,
  [string]$LicenseId = "L_DEV_001"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert($cond, $msg) { if (-not $cond) { throw "ASSERT FAIL: $msg" } }

Write-Host "== Atlas Profiles Smoke (v1) =="
Write-Host "BaseUrl : $BaseUrl"
Write-Host "Tenant  : $TenantId"
Write-Host "Device  : $DeviceId"
Write-Host "License : $LicenseId"
Write-Host ""

# server health
$rootResp = Invoke-RestMethod -Method Get -Uri "$BaseUrl/" -TimeoutSec 10
Assert ($rootResp.ok -eq $true) "root ok"

# Create ProfileExport job (type=100)
$profileId = [Guid]::NewGuid().ToString("N")

$payload = @{
  schema    = "atlas.profile.export.v1"
  profileId = $profileId
  tenantId  = $TenantId
  deviceId  = $DeviceId
  licenseId = $LicenseId
  scope     = "files"
  sourceRoot = "C:/Windows/System32"  # small-ish for smoke
  include   = @("drivers/etc")        # tiny subset
  exclude   = @()
  encryption = @{ mode = "none"; passphraseHint = $null; saltB64 = $null }
  policy     = @{ allow = @("drivers/etc"); deny = @(); maxBytes = 10485760 } # 10MB
} | ConvertTo-Json -Depth 8

$createBody = @{
  tenantId    = $TenantId
  deviceId    = $DeviceId
  type        = 100
  payloadJson = $payload
} | ConvertTo-Json -Depth 8

$created = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs" -ContentType "application/json" -Body $createBody -TimeoutSec 10
Assert ($created.ok -eq $true) "create job ok"
$jobId = $created.job.jobId
Write-Host "Created ProfileExport job: $jobId"
Write-Host "ProfileId: $profileId"
Write-Host ""

Write-Host "NOTE: This smoke assumes your agent is running and polling this deviceId."
Write-Host "Waiting for completion by polling GET /v1/jobs/{id} ..."
Write-Host ""

$deadline = (Get-Date).AddSeconds(60)
do {
  Start-Sleep -Milliseconds 600
  $g = Invoke-RestMethod -Method Get -Uri "$BaseUrl/v1/jobs/$jobId?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
  Assert ($g.ok -eq $true) "get job ok"
  $state = $g.job.state
  if ($state -in 2,3,4) { break }  # Succeeded/Failed/Canceled (adjust if your enum differs)
} while ((Get-Date) -lt $deadline)

$g = Invoke-RestMethod -Method Get -Uri "$BaseUrl/v1/jobs/$jobId?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Write-Host ("Final state: {0}" -f $g.job.state)
Write-Host ("ResultJson: {0}" -f $g.job.resultJson)

Write-Host ""
Write-Host "== DONE =="
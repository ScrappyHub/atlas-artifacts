[CmdletBinding()]
param(
  [string]$BaseUrl  = "http://127.0.0.1:5000",
  [string]$TenantId = "t_dev",
  [string]$DeviceId = $env:COMPUTERNAME
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert($cond, $msg) { if (-not $cond) { throw "ASSERT FAIL: $msg" } }

Write-Host "== Atlas Jobs Smoke (LOCKED v2) =="
Write-Host "BaseUrl : $BaseUrl"
Write-Host "Tenant  : $TenantId"
Write-Host "Device  : $DeviceId"
Write-Host ""

# sanity
[Uri]("$BaseUrl/") | Out-Null
$rootResp = Invoke-RestMethod -Method Get -Uri "$BaseUrl/" -TimeoutSec 10
$health   = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health" -TimeoutSec 10
Assert ($rootResp.ok -eq $true) "root.ok true"
Assert ($health.ok -eq $true)   "health.ok true"
Write-Host "root.ok   = $($rootResp.ok)"
Write-Host "health.ok = $($health.ok)"
Write-Host ""

function New-Job($type = 0) {
  $createBody = @{
    tenantId    = $TenantId
    deviceId    = $DeviceId
    type        = $type
    payloadJson = "{}"
  } | ConvertTo-Json

  $created = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs" -ContentType "application/json" -Body $createBody -TimeoutSec 10
  Assert ($created.ok -eq $true) "create.ok"
  Assert ($null -ne $created.job.jobId) "jobId present"
  return $created.job.jobId
}

# -------- case A: complete from Queued must be 409 (LOCK)
$jobA = New-Job 0
Write-Host "jobA (Queued) = $jobA"

$completeBodyA = @{
  tenantId   = $TenantId
  deviceId   = $DeviceId
  jobId      = $jobA
  ok         = $true
  resultJson = "{}"
} | ConvertTo-Json

try {
  Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/complete" -ContentType "application/json" -Body $completeBodyA -TimeoutSec 10 | Out-Null
  throw "expected 409 invalid_state (complete from queued)"
} catch {
  $resp = $_.Exception.Response
  Assert ($resp.StatusCode.value__ -eq 409) "complete-from-queued returns 409"
}
Write-Host "complete-from-queued => 409 OK"
Write-Host ""

# -------- case B: idempotent start + idempotent cancel request + poll cancel-first + idempotent cancel-ack
$jobB = New-Job 0
Write-Host "jobB = $jobB"

# start twice => ok then already_running (both 200)
$start1 = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/$jobB/start?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Assert ($start1.ok -eq $true) "start1.ok"
$start2 = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/$jobB/start?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Assert ($start2.ok -eq $true) "start2.ok"
Write-Host "start.idempotent codes: $($start1.code), $($start2.code)"

# cancel twice => ok then already_requested (both 200)
$cancel1 = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/$jobB/cancel?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Assert ($cancel1.ok -eq $true) "cancel1.ok"
$cancel2 = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/$jobB/cancel?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Assert ($cancel2.ok -eq $true) "cancel2.ok"
Write-Host "cancel.idempotent codes: $($cancel1.code), $($cancel2.code)"

# create another queued jobC; poll with max=1 must return cancel directive for jobB (cancel-first)
$jobC = New-Job 0
Write-Host "jobC (Queued) = $jobC"

$poll1 = Invoke-RestMethod -Method Get -Uri "$BaseUrl/v1/jobs/poll?tenantId=$TenantId&deviceId=$DeviceId&max=1" -TimeoutSec 10
Assert ($poll1.ok -eq $true) "poll1.ok"
Assert ($poll1.jobs.Count -eq 1) "poll1 returns 1 job"
Assert ($poll1.jobs[0].jobId -eq $jobB) "poll1 returns cancel-first jobB"
Assert ($poll1.jobs[0].cancelRequested -eq $true) "poll1 jobB cancelRequested=true"
Write-Host "poll.cancel-first OK (max=1 returned jobB cancel directive)"
Write-Host ""

# cancel-ack twice => ok then already_canceled (both 200)
$ack1 = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/$jobB/cancel-ack?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Assert ($ack1.ok -eq $true) "ack1.ok"
$ack2 = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/$jobB/cancel-ack?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Assert ($ack2.ok -eq $true) "ack2.ok"
Write-Host "cancel-ack.idempotent codes: $($ack1.code), $($ack2.code)"

# complete-after-cancel must be 409
$completeBodyB = @{
  tenantId   = $TenantId
  deviceId   = $DeviceId
  jobId      = $jobB
  ok         = $true
  resultJson = "{}"
} | ConvertTo-Json

try {
  Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/jobs/complete" -ContentType "application/json" -Body $completeBodyB -TimeoutSec 10 | Out-Null
  throw "expected 409 invalid_state (complete after canceled)"
} catch {
  $resp = $_.Exception.Response
  Assert ($resp.StatusCode.value__ -eq 409) "complete-after-canceled returns 409"
}
Write-Host "complete-after-canceled => 409 OK"
Write-Host ""

# get-job returns terminal state
$getB = Invoke-RestMethod -Method Get -Uri "$BaseUrl/v1/jobs/$jobB?tenantId=$TenantId&deviceId=$DeviceId" -TimeoutSec 10
Assert ($getB.ok -eq $true) "getB.ok"
Assert ($getB.job.state -eq 4) "jobB state is Canceled (4)"
Write-Host "get-job OK (jobB.state = $($getB.job.state))"

Write-Host ""
Write-Host "== DONE (LOCKED v2) =="

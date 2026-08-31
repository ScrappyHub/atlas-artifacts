param(
  [string]$RepoRoot="C:\dev\atlas-update",
  [Parameter(Mandatory=$true)][string]$Target,        # regex matched against updater task/service names
  [string]$WingetId="",                               # optional winget id to pin/unpin
  [ValidateSet("block","unblock")][string]$Action="block",
  [ValidateSet("plan","apply")][string]$Mode="plan",
  [switch]$IUnderstand,
  [switch]$NoSign
)
Set-StrictMode -Version Latest
$ErrorActionPreference="Continue"
$enc=New-Object System.Text.UTF8Encoding($false)
function Ok([string]$m){ Write-Host ("OK: "+$m) -ForegroundColor Green }
function Warn([string]$m){ Write-Host ("WARN: "+$m) -ForegroundColor Yellow }

# never touch core Windows Update / servicing stack from this app-updater blocker
$protectedSvc='^(wuauserv|UsoSvc|WaaSMedicSvc|BITS|TrustedInstaller|CryptSvc|msiserver|DcomLaunch|RpcSs)$'

$isAdmin=$false
try { $isAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator) } catch {}

# ---- discover matching updater tasks + services (capture PRIOR state = rollback record) ----
$taskSteps=@()
try {
  Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -match $Target -or ($_.TaskPath+$_.TaskName) -match $Target } | ForEach-Object {
    $taskSteps += [ordered]@{ kind="scheduled_task"; name=[string]$_.TaskName; path=[string]$_.TaskPath; prior_state=[string]$_.State
      target_state=$(if($Action -eq "block"){"Disabled"}else{"Ready"}) }
  }
} catch {}
$svcSteps=@()
try {
  Get-CimInstance Win32_Service -ErrorAction SilentlyContinue | Where-Object { ($_.Name -match $Target -or $_.DisplayName -match $Target) -and ($_.Name -notmatch $protectedSvc) } | ForEach-Object {
    $svcSteps += [ordered]@{ kind="service"; name=[string]$_.Name; display=[string]$_.DisplayName; prior_start=[string]$_.StartMode; prior_state=[string]$_.State
      target_start=$(if($Action -eq "block"){"Disabled"}else{"Automatic"}) }
  }
} catch {}
$pinSteps=@()
if($WingetId){ $pinSteps += [ordered]@{ kind="winget_pin"; id=$WingetId; op=$(if($Action -eq "block"){"add"}else{"remove"}) } }

$plan=[ordered]@{
  schema="atlas.enforcement.plan.v1"
  utc=[DateTime]::UtcNow.ToString("O")
  hostname=$env:COMPUTERNAME
  action=$Action
  mode=$Mode
  target=$Target
  winget_id=$WingetId
  is_admin=$isAdmin
  requires_admin=([bool]($taskSteps.Count -or $svcSteps.Count))
  steps=@($taskSteps + $svcSteps + $pinSteps)
  step_count=($taskSteps.Count + $svcSteps.Count + $pinSteps.Count)
}
Write-Host ("PLAN: action="+$Action+" mode="+$Mode+" target='"+$Target+"' steps="+$plan.step_count+" admin="+$isAdmin)
foreach($s in $plan.steps){ Write-Host ("  - "+($s | ConvertTo-Json -Compress -Depth 6)) }

$applied=@()
if($Mode -eq "apply"){
  if(-not $IUnderstand){ Warn "apply requires -IUnderstand; refusing to mutate. Re-run with -IUnderstand."; $Mode="plan" }
}
if($Mode -eq "apply"){
  foreach($s in $plan.steps){
    $r=[ordered]@{ step=$s; ok=$false; note="" }
    try {
      switch($s.kind){
        "scheduled_task" {
          if($Action -eq "block"){ Disable-ScheduledTask -TaskName $s.name -TaskPath $s.path -ErrorAction Stop | Out-Null }
          else { Enable-ScheduledTask -TaskName $s.name -TaskPath $s.path -ErrorAction Stop | Out-Null }
          $r.ok=$true; $r.note="task "+$s.name
        }
        "service" {
          if(-not $isAdmin){ $r.note="SKIPPED_NEED_ADMIN"; break }
          if($Action -eq "block"){ Set-Service -Name $s.name -StartupType Disabled -ErrorAction Stop; try{ Stop-Service -Name $s.name -Force -ErrorAction SilentlyContinue }catch{} }
          else { $st=$s.prior_start; $map=@{Auto="Automatic";Manual="Manual";Disabled="Disabled"}; $ns=$(if($map.ContainsKey($st)){$map[$st]}else{"Manual"}); Set-Service -Name $s.name -StartupType $ns -ErrorAction Stop }
          $r.ok=$true; $r.note="service "+$s.name
        }
        "winget_pin" {
          $wg=Get-Command winget.exe -ErrorAction SilentlyContinue; $wgp=$(if($wg){$wg.Source}else{Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"})
          if($s.op -eq "add"){ & $wgp pin add --id $s.id --accept-source-agreements 2>&1 | Out-Null } else { & $wgp pin remove --id $s.id 2>&1 | Out-Null }
          $r.ok=$true; $r.note="winget pin "+$s.op+" "+$s.id
        }
      }
    } catch { $r.note="ERROR: "+$_.Exception.Message }
    Write-Host ("  APPLIED "+($r.note)+" ok="+$r.ok)
    $applied += $r
  }
}

$result=[ordered]@{
  schema="atlas.enforcement.result.v1"
  utc=[DateTime]::UtcNow.ToString("O")
  plan=$plan
  mode=$Mode
  applied=$applied
  rollback_hint="re-run with -Action "+$(if($Action -eq 'block'){'unblock'}else{'block'})+" -Mode apply -IUnderstand to reverse (prior_state captured per step)"
}
$dir=Join-Path $RepoRoot "runtime\software_fleet\enforcement"
if(-not (Test-Path -LiteralPath $dir)){ New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$ts=[DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ")
$rp=Join-Path $dir ("enforce_"+$Action+"_"+$Mode+"_"+$ts+".json")
[IO.File]::WriteAllText($rp,($result | ConvertTo-Json -Depth 12).Replace("`r`n","`n"),$enc)
Ok ("ENFORCEMENT_RECORD="+$rp)

# desired-state ledger for the persistent reconcile task (increment 3)
if($Mode -eq "apply"){
  $ds=Join-Path $RepoRoot "runtime\software_fleet\enforcement_desired_state.ndjson"
  $line=([ordered]@{ utc=$result.utc; target=$Target; winget_id=$WingetId; action=$Action } | ConvertTo-Json -Compress -Depth 6)
  Add-Content -LiteralPath $ds -Value $line -Encoding utf8
}

if(-not $NoSign){
  Push-Location $RepoRoot
  try { $out=& dotnet run --project "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj" -c Debug -- emit-artifact --file $rp --event-type "atlas.enforcement.result.v1" --strength evidence --tag enforcement --tag windows 2>&1 } finally { Pop-Location }
  ($out | Where-Object {$_ -match '^(EMIT_OK|CONTENT_REF|PACKET_ID)='}) | ForEach-Object { Write-Host $_ }
}
Write-Host "ATLAS_ENFORCE_OK" -ForegroundColor Green

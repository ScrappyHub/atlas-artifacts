param(
  [string]$RepoRoot="C:\dev\atlas-update",
  [Parameter(Mandatory=$true)][string]$Id,
  [ValidateSet("dry","apply")][string]$Mode="dry",
  [string]$Provider="winget",
  [switch]$IUnderstand,
  [switch]$NoSign
)
Set-StrictMode -Version Latest; $ErrorActionPreference="Continue"
$enc=New-Object System.Text.UTF8Encoding($false)
function Ok($m){ Write-Host ("OK: "+$m) -ForegroundColor Green }
function ArrOf($v){ if($null -eq $v){@()} elseif($v -is [string]){@($v)} else {@($v)} }

# --- policy gate (held / blacklisted / protected => refuse) ---
$polPath=Join-Path $RepoRoot "runtime\software_fleet\windows_policy.json"
$held=@(); $black=@(); $prot=@()
if(Test-Path -LiteralPath $polPath){ try { $pol=Get-Content -Raw $polPath|ConvertFrom-Json
  $held=ArrOf $pol.hold; $black=ArrOf $pol.blacklisted; $prot=ArrOf $pol.protected } catch {} }
$state="allowed"
if($black -contains $Id){ $state="blocked_blacklisted" }
elseif($prot -contains $Id){ $state="blocked_protected" }
elseif($held -contains $Id){ $state="held" }
$allowed = ($state -eq "allowed")

# --- resolve current installed version (best-effort via winget) ---
$wg=Get-Command winget.exe -EA SilentlyContinue; $wgp=$(if($wg){$wg.Source}else{Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"})
$curVer=""
try { $lst=(& $wgp list --id $Id --exact --accept-source-agreements 2>&1 | Out-String)
  foreach($ln in ($lst -split "`n")){ $mm=[regex]::Match($ln,'\s(\d+(?:\.\d+){1,3}[\w\.\-]*)\s'); if($mm.Success){ $curVer=$mm.Groups[1].Value; break } } } catch {}

$action="dry_run"; $exit=0; $stdoutTail=""
if($Mode -eq "apply"){
  if(-not $allowed){ $action="blocked" }
  elseif(-not $IUnderstand){ $action="dry_run"; Write-Host "apply requires -IUnderstand; staying dry" -ForegroundColor Yellow }
  else {
    $action="execute"
    try {
      $o=(& $wgp upgrade --id $Id --exact --accept-source-agreements --accept-package-agreements 2>&1 | Out-String)
      $exit=$LASTEXITCODE; $stdoutTail=(($o -split "`n") | Select-Object -Last 6) -join " | "
    } catch { $exit=1; $stdoutTail="ERROR: "+$_.Exception.Message }
  }
}

$res=[ordered]@{
  schema="atlas.update.result.v1"; utc=[DateTime]::UtcNow.ToString("O"); hostname=$env:COMPUTERNAME
  id=$Id; provider=$Provider; policy_state=$state; allowed=$allowed; mode=$Mode; action=$action
  current_version=$curVer; exit_code=$exit; stdout_tail=$stdoutTail
  rollback_cmd=$(if($curVer){"winget install --id $Id --exact --version $curVer --accept-source-agreements --accept-package-agreements"}else{""})
}
$dir=Join-Path $RepoRoot "runtime\software_fleet\updates"; if(-not(Test-Path -LiteralPath $dir)){New-Item -ItemType Directory -Force -Path $dir|Out-Null}
$ts=[DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"); $rp=Join-Path $dir ("update_"+($Id -replace '[^\w\.\-]','_')+"_"+$Mode+"_"+$ts+".json")
[IO.File]::WriteAllText($rp,($res|ConvertTo-Json -Depth 8).Replace("`r`n","`n"),$enc)
Write-Host ("UPDATE id="+$Id+" state="+$state+" allowed="+$allowed+" action="+$action+" cur="+$curVer+" exit="+$exit)
Ok ("UPDATE_RECORD="+$rp)
if(-not $NoSign){ Push-Location $RepoRoot; try { & dotnet run --project "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj" -c Debug -- emit-artifact --file $rp --event-type "atlas.update.result.v1" --strength evidence --tag update 2>&1 | Where-Object {$_ -match '^(EMIT_OK|PACKET_ID)='} | ForEach-Object{Write-Host $_} } finally { Pop-Location } }
if($Mode -eq "apply" -and -not $allowed){ Write-Host ("ATLAS_UPDATE_BLOCKED: "+$state) -ForegroundColor Yellow } else { Write-Host "ATLAS_UPDATE_OK" -ForegroundColor Green }

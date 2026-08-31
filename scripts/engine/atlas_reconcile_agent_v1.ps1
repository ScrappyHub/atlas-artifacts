param([string]$RepoRoot="C:\dev\atlas-update",[switch]$NoSign)
Set-StrictMode -Version Latest
$ErrorActionPreference="Continue"
$enc=New-Object System.Text.UTF8Encoding($false)
$protectedSvc='^(wuauserv|UsoSvc|WaaSMedicSvc|BITS|TrustedInstaller|CryptSvc|msiserver|DcomLaunch|RpcSs)$'
$ds=Join-Path $RepoRoot "runtime\software_fleet\enforcement_desired_state.ndjson"
$desired=@{}
if(Test-Path -LiteralPath $ds){
  foreach($l in (Get-Content -LiteralPath $ds)){ if(-not $l.Trim()){continue}; try { $o=$l|ConvertFrom-Json; $desired[[string]$o.target]=$o } catch {} }
}
$actions=@()
foreach($t in $desired.Keys){
  $o=$desired[$t]; if([string]$o.action -ne "block"){ continue }   # unblock = stop enforcing
  try {
    Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -match $t } | ForEach-Object {
      if([string]$_.State -ne "Disabled"){ try { Disable-ScheduledTask -TaskName $_.TaskName -TaskPath $_.TaskPath -EA Stop|Out-Null; $actions+=[ordered]@{kind="task";name=$_.TaskName;reconciled=$true} } catch { $actions+=[ordered]@{kind="task";name=$_.TaskName;error=$_.Exception.Message} } }
    }
  } catch {}
  try {
    Get-CimInstance Win32_Service -EA SilentlyContinue | Where-Object { ($_.Name -match $t -or $_.DisplayName -match $t) -and ($_.Name -notmatch $protectedSvc) } | ForEach-Object {
      if([string]$_.StartMode -ne "Disabled"){ try { Set-Service -Name $_.Name -StartupType Disabled -EA Stop; $actions+=[ordered]@{kind="service";name=$_.Name;reconciled=$true} } catch { $actions+=[ordered]@{kind="service";name=$_.Name;error=$_.Exception.Message} } }
    }
  } catch {}
  if([string]$o.winget_id){ try { $wg=Get-Command winget.exe -EA SilentlyContinue; $wgp=$(if($wg){$wg.Source}else{Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"}); & $wgp pin add --id $o.winget_id --accept-source-agreements 2>&1|Out-Null; $actions+=[ordered]@{kind="pin";id=$o.winget_id;reconciled=$true} } catch {} }
}
$rec=[ordered]@{ schema="atlas.reconcile.v1"; utc=[DateTime]::UtcNow.ToString("O"); hostname=$env:COMPUTERNAME; desired_targets=@($desired.Keys); reconciled_count=$actions.Count; actions=$actions }
$dir=Join-Path $RepoRoot "runtime\software_fleet\reconcile"; if(-not(Test-Path -LiteralPath $dir)){New-Item -ItemType Directory -Force -Path $dir|Out-Null}
$ts=[DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"); $rp=Join-Path $dir ("reconcile_"+$ts+".json")
[IO.File]::WriteAllText($rp,($rec|ConvertTo-Json -Depth 10).Replace("`r`n","`n"),$enc)
Write-Host ("RECONCILE actions="+$actions.Count+" targets="+@($desired.Keys).Count)
if(-not $NoSign){ Push-Location $RepoRoot; try { & dotnet run --project "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj" -c Debug -- emit-artifact --file $rp --event-type "atlas.reconcile.v1" --strength evidence --tag reconcile 2>&1 | Where-Object {$_ -match '^(EMIT_OK|PACKET_ID)='} | ForEach-Object{Write-Host $_} } finally { Pop-Location } }
Write-Host "ATLAS_RECONCILE_OK" -ForegroundColor Green

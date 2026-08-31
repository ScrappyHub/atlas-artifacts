param([string]$RepoRoot="C:\dev\atlas-update",[ValidateSet("install","uninstall")][string]$Op="install",[int]$IntervalMinutes=60)
Set-StrictMode -Version Latest; $ErrorActionPreference="Stop"
$name="AtlasUpdateGuard"
$isAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if(-not $isAdmin){ Write-Host "NEED_ADMIN: run elevated" -ForegroundColor Red; exit 2 }
if($Op -eq "uninstall"){
  try { Unregister-ScheduledTask -TaskName $name -Confirm:$false; Write-Host "ATLAS_GUARD_UNINSTALLED" -ForegroundColor Green } catch { Write-Host ("uninstall: "+$_.Exception.Message) }
  exit 0
}
$agent=Join-Path $RepoRoot "scripts\engine\atlas_reconcile_agent_v1.ps1"
$action=New-ScheduledTaskAction -Execute "powershell.exe" -Argument ("-NoProfile -ExecutionPolicy Bypass -File `""+$agent+"`" -RepoRoot `""+$RepoRoot+"`" -NoSign")
$t1=New-ScheduledTaskTrigger -AtStartup
$t2=New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes)
$principal=New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Register-ScheduledTask -TaskName $name -Action $action -Trigger @($t1,$t2) -Principal $principal -Settings $settings -Force | Out-Null
Write-Host ("ATLAS_GUARD_INSTALLED interval="+$IntervalMinutes+"m runs=SYSTEM (startup + repeating)") -ForegroundColor Green

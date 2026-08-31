param([string]$RepoRoot="C:\dev\atlas-update",[string]$Target="Opera GX scheduled")
Set-StrictMode -Version Latest; $ErrorActionPreference="Continue"
$enc=New-Object System.Text.UTF8Encoding($false)
$ts=[DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ")
$run=Join-Path $RepoRoot ("proofs\audit\updater_green_"+$ts); New-Item -ItemType Directory -Force -Path $run|Out-Null
$summary=Join-Path $run "summary.txt"
function Log($m){ Write-Host $m; Add-Content -LiteralPath $summary -Value $m -Encoding utf8 }
function Tasks(){ @(Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -match $Target }) }
function States(){ @(Tasks | ForEach-Object { [string]$_.State }) }
function Enf($action){ & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot "scripts\engine\atlas_enforce_block_v1.ps1") -RepoRoot $RepoRoot -Target $Target -Action $action -Mode apply -IUnderstand 2>&1 | Out-File -FilePath (Join-Path $run ("enforce_"+$action+".txt")) -Encoding utf8 }

$isAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
Log ("ATLAS UPDATER WINDOWS GREEN "+$ts)
Log ("target='"+$Target+"' is_admin="+$isAdmin)
if(-not $isAdmin){ Log "FAIL: not elevated (disable/enable scheduled task needs admin)"; Log "ATLAS_UPDATER_WINDOWS_GREEN_FAIL"; exit 2 }

$pre=States; Log ("PRE states=["+($pre -join ",")+"] count="+$pre.Count)
if($pre.Count -lt 1){ Log ("FAIL: no updater tasks match '"+$Target+"'"); Log "ATLAS_UPDATER_WINDOWS_GREEN_FAIL"; exit 2 }

# [1] BLOCK
Enf "block"; $b=States; $blocked=(@($b|Where-Object{$_ -ne "Disabled"}).Count -eq 0)
Log ("BLOCK  -> ["+($b -join ",")+"]  blocked="+$blocked)

# [2] SIMULATE DRIFT: app re-enables its own updater task
$first=(Tasks|Select-Object -First 1)
if($first){ Enable-ScheduledTask -TaskName $first.TaskName -TaskPath $first.TaskPath -ErrorAction SilentlyContinue|Out-Null }
$d=States; $drifted=(@($d|Where-Object{$_ -eq "Ready"}).Count -ge 1)
Log ("DRIFT  -> ["+($d -join ",")+"]  drift_created="+$drifted)

# [3] RECONCILE (passive re-enforcement)
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot "scripts\engine\atlas_reconcile_agent_v1.ps1") -RepoRoot $RepoRoot 2>&1 | Out-File -FilePath (Join-Path $run "reconcile.txt") -Encoding utf8
$r=States; $reconciled=(@($r|Where-Object{$_ -ne "Disabled"}).Count -eq 0)
Log ("RECON  -> ["+($r -join ",")+"]  reenforced="+$reconciled)

# [4] UNBLOCK (rollback to prior)
Enf "unblock"; $u=States; $restored=(@($u|Where-Object{$_ -ne "Ready"}).Count -eq 0)
Log ("UNBLOCK-> ["+($u -join ",")+"]  restored="+$restored)

$green = $blocked -and $drifted -and $reconciled -and $restored
Log ""
Log ("CHECK blocked="+$blocked+" drift="+$drifted+" reenforced="+$reconciled+" restored="+$restored)
if($green){ Log "ATLAS_UPDATER_WINDOWS_GREEN_OK" } else { Log "ATLAS_UPDATER_WINDOWS_GREEN_FAIL" }
Copy-Item -LiteralPath $summary -Destination (Join-Path $RepoRoot "proofs\audit\LAST_UPDATER_GREEN.txt") -Force
Log ("RUN_DIR="+$run)

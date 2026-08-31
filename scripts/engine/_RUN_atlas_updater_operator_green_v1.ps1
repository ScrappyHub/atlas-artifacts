param([string]$RepoRoot="C:\dev\atlas-update",[string]$Target="Opera GX scheduled")
Set-StrictMode -Version Latest; $ErrorActionPreference="Continue"
$ts=[DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"); $run=Join-Path $RepoRoot ("proofs\audit\updater_operator_"+$ts)
New-Item -ItemType Directory -Force -Path $run|Out-Null; $sum=Join-Path $run "summary.txt"
function Log($m){ Write-Host $m; Add-Content -LiteralPath $sum -Value $m -Encoding utf8 }
function Sub($name,$file,$argl){ $o=(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot $file) @argl 2>&1); $o|Out-File -FilePath (Join-Path $run ($name+".txt")) -Encoding utf8; return ($o -join "`n") }
$eng="scripts\engine\"; $checks=[ordered]@{}
$isAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
Log ("ATLAS UPDATER OPERATOR GREEN "+$ts+" admin="+$isAdmin)

$o=Sub "inventory" ($eng+"atlas_software_inventory_full_v1.ps1") @("-RepoRoot",$RepoRoot)
$checks["inventory"]=[bool]($o -match "ATLAS_FULL_INVENTORY_OK" -and $o -match "SIGNATURE_VALID")

$o=Sub "update_blocked" ($eng+"atlas_update_apply_v1.ps1") @("-RepoRoot",$RepoRoot,"-Id","BlenderFoundation.Blender","-Mode","apply","-IUnderstand")
$checks["update_gate_blocks_held"]=[bool]($o -match "ATLAS_UPDATE_BLOCKED")

$o=Sub "update_dry" ($eng+"atlas_update_apply_v1.ps1") @("-RepoRoot",$RepoRoot,"-Id","7zip.7zip","-Mode","dry")
$checks["update_dry_allowed"]=[bool]($o -match "ATLAS_UPDATE_OK")

$o=Sub "rollback_dry" ($eng+"atlas_rollback_version_v1.ps1") @("-RepoRoot",$RepoRoot,"-Id","7zip.7zip","-Version","24.09","-Mode","dry")
$checks["rollback_dry"]=[bool]($o -match "ATLAS_ROLLBACK_OK")

$o=Sub "lifecycle" ($eng+"_RUN_atlas_updater_windows_green_v1.ps1") @("-RepoRoot",$RepoRoot,"-Target",$Target)
$checks["passive_block_lifecycle"]=[bool]($o -match "ATLAS_UPDATER_WINDOWS_GREEN_OK")

$o=Sub "os_scan" ($eng+"atlas_windows_update_adapter_v1.ps1") @("-Command","scan","-RepoRoot",$RepoRoot)
$checks["os_update_scan_ran"]=[bool]($o -match "ATLAS_WINDOWS_UPDATE_SCAN_OK")

Log ""
foreach($k in $checks.Keys){ Log ("CHECK "+$k+"="+$checks[$k]) }
$critical=@("inventory","update_gate_blocks_held","update_dry_allowed","rollback_dry","passive_block_lifecycle")
$green=$true; foreach($c in $critical){ if(-not $checks[$c]){ $green=$false } }
Log ""
if($green){ Log "ATLAS_UPDATER_OPERATOR_GREEN_OK" } else { Log "ATLAS_UPDATER_OPERATOR_GREEN_FAIL" }
Copy-Item -LiteralPath $sum -Destination (Join-Path $RepoRoot "proofs\audit\LAST_OPERATOR_GREEN.txt") -Force
Log ("RUN_DIR="+$run)

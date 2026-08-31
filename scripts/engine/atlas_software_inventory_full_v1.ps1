param([string]$RepoRoot="C:\dev\atlas-update",[switch]$NoSign)
Set-StrictMode -Version Latest
$ErrorActionPreference="Continue"
function Ok([string]$m){ Write-Host ("OK: "+$m) -ForegroundColor Green }
$enc=New-Object System.Text.UTF8Encoding($false)
$apps=New-Object System.Collections.Generic.List[object]

# --- Win32 apps: robust per-subkey enumeration across all four uninstall hives ---
$hives=@(
 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
 'HKCU:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
foreach($h in $hives){
  if(-not (Test-Path -LiteralPath $h)){ continue }
  Get-ChildItem -LiteralPath $h -ErrorAction SilentlyContinue | ForEach-Object {
    $pp=$null; try { $pp=Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue } catch {}
    if($pp -and ($pp.PSObject.Properties.Name -contains 'DisplayName') -and $pp.DisplayName){
      $sc=0; if($pp.PSObject.Properties.Name -contains 'SystemComponent'){ $sc=[int]$pp.SystemComponent }
      $apps.Add([ordered]@{
        name=[string]$pp.DisplayName
        version=$(if($pp.PSObject.Properties.Name -contains 'DisplayVersion'){[string]$pp.DisplayVersion}else{""})
        publisher=$(if($pp.PSObject.Properties.Name -contains 'Publisher'){[string]$pp.Publisher}else{""})
        install_date=$(if($pp.PSObject.Properties.Name -contains 'InstallDate'){[string]$pp.InstallDate}else{""})
        uninstall=$(if($pp.PSObject.Properties.Name -contains 'UninstallString'){[string]$pp.UninstallString}else{""})
        system_component=$sc
        source="registry"
        id=[string]$_.PSChildName
      })
    }
  }
}
# --- Store / MSIX apps ---
try {
  Get-AppxPackage -ErrorAction SilentlyContinue | ForEach-Object {
    $apps.Add([ordered]@{ name=[string]$_.Name; version=[string]$_.Version; publisher=[string]$_.Publisher; install_date=""; uninstall=""; system_component=0; source="appx"; id=[string]$_.PackageFullName })
  }
} catch {}
$installed=@($apps | Sort-Object -Property @{e={[string]$_.name}},@{e={[string]$_.version}})

# --- winget: resolve explicitly (App Execution Alias often not on Get-Command PATH) ---
$wgPath=$null
$c=Get-Command winget.exe -ErrorAction SilentlyContinue
if($c){ $wgPath=$c.Source } else {
  $cand=Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"
  if(Test-Path -LiteralPath $cand){ $wgPath=$cand }
}
$wingetFound=[bool]$wgPath
$wingetListRaw=""; $wingetUpgradeRaw=""; $wingetPinsRaw=""
if($wgPath){
  try { $wingetListRaw=(& $wgPath list --accept-source-agreements 2>&1 | Out-String) } catch { $wingetListRaw="ERROR: "+$_.Exception.Message }
  try { $wingetUpgradeRaw=(& $wgPath upgrade --include-unknown --accept-source-agreements 2>&1 | Out-String) } catch { $wingetUpgradeRaw="ERROR: "+$_.Exception.Message }
  try { $wingetPinsRaw=(& $wgPath pin list 2>&1 | Out-String) } catch { $wingetPinsRaw="ERROR: "+$_.Exception.Message }
}

# --- updater scheduled tasks + services (targets for passive blocking) ---
$tasks=@()
try { Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { ($_.TaskName -match '(?i)update|upgrade') -or ($_.TaskPath -match '(?i)update|google|edge|adobe|mozilla|brave|opera|nvidia') } | ForEach-Object { $tasks += [ordered]@{ name=[string]$_.TaskName; path=[string]$_.TaskPath; state=[string]$_.State } } } catch {}
$svcs=@()
try { Get-Service -ErrorAction SilentlyContinue | Where-Object { ($_.Name -match '(?i)update|upgrade|gupdate|edgeupdate|brave') -or ($_.DisplayName -match '(?i)update') } | ForEach-Object { $svcs += [ordered]@{ name=[string]$_.Name; display=[string]$_.DisplayName; status=[string]$_.Status; start=[string]$_.StartType } } } catch {}

$os=$null; try { $os=Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue } catch {}
$bySource=@{}; foreach($a in $installed){ $s=[string]$a.source; if(-not $bySource.ContainsKey($s)){$bySource[$s]=0}; $bySource[$s]++ }

$inv=[ordered]@{
  schema="atlas.software_inventory.full.v1"
  captured_utc=[DateTime]::UtcNow.ToString("O")
  hostname=$env:COMPUTERNAME
  os_caption=$(if($os){[string]$os.Caption}else{""})
  os_version=$(if($os){[string]$os.Version}else{[Environment]::OSVersion.Version.ToString()})
  winget_found=$wingetFound
  counts=[ordered]@{ installed=$installed.Count; registry=[int]($(if($bySource.ContainsKey('registry')){$bySource['registry']}else{0})); appx=[int]($(if($bySource.ContainsKey('appx')){$bySource['appx']}else{0})); updater_tasks=$tasks.Count; updater_services=$svcs.Count }
  installed=$installed
  updater_tasks=$tasks
  updater_services=$svcs
  winget_list_raw=$wingetListRaw
  winget_upgrade_raw=$wingetUpgradeRaw
  winget_pins_raw=$wingetPinsRaw
}
$json=($inv | ConvertTo-Json -Depth 8).Replace("`r`n","`n")
$dir=Join-Path $RepoRoot "runtime\software_fleet\full_inventory"
if(-not (Test-Path -LiteralPath $dir)){ New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$ts=[DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ")
$path=Join-Path $dir ("full_inventory_"+$ts+".json")
[IO.File]::WriteAllText($path,$json,$enc)
Ok ("INVENTORY_JSON="+$path)
Ok ("INSTALLED="+$installed.Count+" (registry="+$inv.counts.registry+" appx="+$inv.counts.appx+") winget_found="+$wingetFound+" tasks="+$tasks.Count+" services="+$svcs.Count)

if(-not $NoSign){
  Push-Location $RepoRoot
  try {
    $out = & dotnet run --project "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj" -c Debug -- emit-artifact --file $path --event-type "atlas.software_inventory.full.v1" --strength evidence --tag inventory --tag windows 2>&1
  } finally { Pop-Location }
  $out | ForEach-Object { Write-Host $_ }
  $pk=(@($out | Where-Object {$_ -match '^PACKET_ID='}) -replace '^PACKET_ID=','')
  if($pk){
    $pkHex=$pk -replace '^sha256:',''
    $pdir=Join-Path $RepoRoot ("data\outbox\"+$pkHex)
    Ok ("SIGNED_INVENTORY_PACKET="+$pk)
    Write-Host "--- verify signed inventory packet ---"
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot "scripts\_VERIFY_atlas_signed_inventory_packet_v1.ps1") -RepoRoot $RepoRoot -PacketDir $pdir 2>&1 | ForEach-Object { Write-Host $_ }
  } else { Write-Host "SIGN_FAILED - see output above" -ForegroundColor Red }
}
Write-Host "ATLAS_FULL_INVENTORY_OK" -ForegroundColor Green

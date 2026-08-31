param(
  [string]$RepoRoot="C:\dev\atlas-update",
  [Parameter(Mandatory=$true)][string]$Id,
  [Parameter(Mandatory=$true)][string]$Version,
  [ValidateSet("dry","apply")][string]$Mode="dry",
  [switch]$IUnderstand,[switch]$NoSign
)
Set-StrictMode -Version Latest; $ErrorActionPreference="Continue"
$enc=New-Object System.Text.UTF8Encoding($false)
$wg=Get-Command winget.exe -EA SilentlyContinue; $wgp=$(if($wg){$wg.Source}else{Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"})
$cmd="winget install --id $Id --exact --version $Version --accept-source-agreements --accept-package-agreements"
$action="dry_run"; $exit=0; $tail=""
if($Mode -eq "apply"){
  if(-not $IUnderstand){ Write-Host "apply requires -IUnderstand; staying dry" -ForegroundColor Yellow }
  else { $action="execute"; try { $o=(& $wgp install --id $Id --exact --version $Version --accept-source-agreements --accept-package-agreements 2>&1|Out-String); $exit=$LASTEXITCODE; $tail=(($o -split "`n")|Select-Object -Last 6) -join " | " } catch { $exit=1; $tail="ERROR: "+$_.Exception.Message } }
}
$res=[ordered]@{ schema="atlas.rollback.result.v1"; utc=[DateTime]::UtcNow.ToString("O"); hostname=$env:COMPUTERNAME; id=$Id; target_version=$Version; mode=$Mode; action=$action; command=$cmd; exit_code=$exit; stdout_tail=$tail }
$dir=Join-Path $RepoRoot "runtime\software_fleet\rollbacks"; if(-not(Test-Path -LiteralPath $dir)){New-Item -ItemType Directory -Force -Path $dir|Out-Null}
$ts=[DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"); $rp=Join-Path $dir ("rollback_"+($Id -replace '[^\w\.\-]','_')+"_"+$ts+".json")
[IO.File]::WriteAllText($rp,($res|ConvertTo-Json -Depth 8).Replace("`r`n","`n"),$enc)
Write-Host ("ROLLBACK id="+$Id+" -> version "+$Version+" action="+$action+" exit="+$exit)
if(-not $NoSign){ Push-Location $RepoRoot; try { & dotnet run --project "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj" -c Debug -- emit-artifact --file $rp --event-type "atlas.rollback.result.v1" --strength evidence --tag rollback 2>&1 | Where-Object {$_ -match '^(EMIT_OK|PACKET_ID)='}|ForEach-Object{Write-Host $_} } finally { Pop-Location } }
Write-Host "ATLAS_ROLLBACK_OK" -ForegroundColor Green

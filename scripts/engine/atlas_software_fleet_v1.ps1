param(
  [Parameter(Mandatory=$true)]
  [ValidateSet("inventory","update-plan","rollback-plan","hold","allow","jobs","doctor")]
  [string]$Command,

  [string]$RepoRoot = "C:\dev\atlas-update",
  [string]$DevRoot = "C:\dev",
  [string]$Name = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_SOFTWARE_FLEET_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Ensure-Dir([string]$Path){
  if(-not (Test-Path -LiteralPath $Path -PathType Container)){
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
  }
}

function Write-Utf8NoBomLf([string]$Path,[string]$Text){
  $dir = Split-Path -Parent $Path
  if($dir){ Ensure-Dir $dir }
  $t = $Text.Replace("`r`n","`n").Replace("`r","`n")
  if(-not $t.EndsWith("`n")){ $t += "`n" }
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($Path,$t,$enc)
}

function Append-Ndjson([string]$Path,[object]$Obj){
  $json = $Obj | ConvertTo-Json -Compress -Depth 20
  $old = ""
  if(Test-Path -LiteralPath $Path -PathType Leaf){
    $old = [System.IO.File]::ReadAllText($Path,[System.Text.UTF8Encoding]::new($false))
    $old = $old.Replace("`r`n","`n").Replace("`r","`n")
    if($old.Length -gt 0 -and -not $old.EndsWith("`n")){ $old += "`n" }
  }
  Write-Utf8NoBomLf $Path ($old + $json)
}

function Run-Git([string]$Repo,[string[]]$Args){
  if(-not (Test-Path -LiteralPath $Repo -PathType Container)){
    return [pscustomobject]@{ code=9001; text="REPO_PATH_MISSING: $Repo"; ok=$false }
  }

  $git = (Get-Command git.exe -ErrorAction Stop).Source

  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $git
  $psi.WorkingDirectory = $Repo
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true

  foreach($a in @($Args)){
    [void]$psi.ArgumentList.Add([string]$a)
  }

  $p = [System.Diagnostics.Process]::Start($psi)
  $stdout = $p.StandardOutput.ReadToEnd()
  $stderr = $p.StandardError.ReadToEnd()
  $p.WaitForExit()

  $text = (($stdout + "`n" + $stderr).Replace("`r`n","`n").Replace("`r","`n")).Trim()

  [pscustomobject]@{
    code = $p.ExitCode
    text = $text
    ok = ($p.ExitCode -eq 0)
  }
}

function Get-RepoState([string]$Path){
  $name = Split-Path -Leaf $Path
  $branchResult = Run-Git $Path @("rev-parse","--abbrev-ref","HEAD")
  $headResult = Run-Git $Path @("rev-parse","HEAD")
  $remoteResult = Run-Git $Path @("remote","get-url","origin")
  $statusResult = Run-Git $Path @("status","--short")
  $upstreamResult = Run-Git $Path @("rev-parse","--abbrev-ref","--symbolic-full-name","@{u}")

  $branch = if($branchResult.ok){ $branchResult.text.Trim() } else { "UNKNOWN_BRANCH" }
  $head = if($headResult.ok){ $headResult.text.Trim() } else { "UNKNOWN_HEAD" }
  $remote = if($remoteResult.ok){ $remoteResult.text.Trim() } else { "" }
  $status = if($statusResult.ok){ $statusResult.text } else { "STATUS_FAILED: " + $statusResult.text }
  $upstream = if($upstreamResult.ok){ $upstreamResult.text.Trim() } else { "" }
  $repoState = Get-AtlasRepoStateV1 $branchResult $headResult $remoteResult $statusResult $upstreamResult
  $gitError = Join-AtlasGitErrorsV1 @($branchResult,$headResult,$remoteResult,$statusResult,$upstreamResult)

  [pscustomobject][ordered]@{
    name = $name
    path = $Path
    branch = $branch
    head = $head
    remote = $remote
    upstream = $upstream
    repo_state = $repoState
    git_error  = $gitError
    dirty = -not [string]::IsNullOrWhiteSpace($status)
    status_short = $status
  }
}

if(-not (Test-Path -LiteralPath $RepoRoot -PathType Container)){ Die ("MISSING_REPOROOT: " + $RepoRoot) }
if(-not (Test-Path -LiteralPath $DevRoot -PathType Container)){ Die ("MISSING_DEVROOT: " + $DevRoot) }

$StateDir = Join-Path $RepoRoot "runtime\software_fleet"
$ReceiptDir = Join-Path $RepoRoot "proofs\receipts\software_fleet"
Ensure-Dir $StateDir
Ensure-Dir $ReceiptDir

$InventoryPath = Join-Path $StateDir "inventory.latest.json"
$AllowPath = Join-Path $StateDir "allow_update.txt"
$HoldPath = Join-Path $StateDir "hold_update.txt"
$JobsPath = Join-Path $StateDir "jobs.ndjson"
$ReceiptsPath = Join-Path $ReceiptDir "software_fleet.ndjson"

switch($Command){
  "doctor" {
    if(-not (Get-Command git.exe -ErrorAction SilentlyContinue)){ Die "GIT_NOT_FOUND" }
    Ok ("REPOROOT=" + $RepoRoot)
    Ok ("DEVROOT=" + $DevRoot)
    Ok ("STATE_DIR=" + $StateDir)
    Write-Host "ATLAS_SOFTWARE_FLEET_DOCTOR_OK" -ForegroundColor Green
  }

  "inventory" {
    $repos = @()
    foreach($dir in @(Get-ChildItem -LiteralPath $DevRoot -Directory -ErrorAction Stop)){
      $gitDir = Join-Path $dir.FullName ".git"
      if(Test-Path -LiteralPath $gitDir){
        $repos += Get-RepoState $dir.FullName
      }
    }

    $obj = [ordered]@{
      schema = "atlas.software_fleet.inventory.v1"
      utc = [DateTime]::UtcNow.ToString("O")
      dev_root = $DevRoot
      count = @($repos).Count
      repos = $repos
    }

    Write-Utf8NoBomLf $InventoryPath ($obj | ConvertTo-Json -Depth 30)
    Append-Ndjson $ReceiptsPath ([ordered]@{
      type="atlas.software_fleet.receipt.v1"
      action="inventory"
      utc=[DateTime]::UtcNow.ToString("O")
      count=@($repos).Count
      inventory_path=$InventoryPath
    })

    Ok ("INVENTORY_PATH=" + $InventoryPath)
    Ok ("REPO_COUNT=" + @($repos).Count)
    Write-Host "ATLAS_SOFTWARE_FLEET_INVENTORY_OK" -ForegroundColor Green
  }

  "hold" {
    if([string]::IsNullOrWhiteSpace($Name)){ Die "HOLD_REQUIRES_NAME" }
    $old = ""
    if(Test-Path $HoldPath){ $old = Get-Content -LiteralPath $HoldPath -Raw }
    if($old -notmatch [regex]::Escape($Name)){
      Write-Utf8NoBomLf $HoldPath (($old.TrimEnd() + "`n" + $Name).TrimStart())
    }
    Ok ("HELD=" + $Name)
    Write-Host "ATLAS_SOFTWARE_FLEET_HOLD_OK" -ForegroundColor Green
  }

  "allow" {
    if([string]::IsNullOrWhiteSpace($Name)){ Die "ALLOW_REQUIRES_NAME" }
    $old = ""
    if(Test-Path $AllowPath){ $old = Get-Content -LiteralPath $AllowPath -Raw }
    if($old -notmatch [regex]::Escape($Name)){
      Write-Utf8NoBomLf $AllowPath (($old.TrimEnd() + "`n" + $Name).TrimStart())
    }
    Ok ("ALLOWED=" + $Name)
    Write-Host "ATLAS_SOFTWARE_FLEET_ALLOW_OK" -ForegroundColor Green
  }

  "update-plan" {
    if(-not (Test-Path $InventoryPath)){ Die "RUN_INVENTORY_FIRST" }
    $inv = Get-Content -LiteralPath $InventoryPath -Raw | ConvertFrom-Json

    $holds = @()
    if(Test-Path $HoldPath){ $holds = @(Get-Content -LiteralPath $HoldPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) }

    foreach($r in @($inv.repos)){
      $decision = "plan_update"
      if($holds -contains $r.name){ $decision = "held_no_update" }
      if($r.dirty){ $decision = "dirty_no_update" }

      Append-Ndjson $JobsPath ([ordered]@{
        schema="atlas.software_fleet.job.v1"
        utc=[DateTime]::UtcNow.ToString("O")
        job_type="update_plan"
        repo=$r.name
        path=$r.path
        branch=$r.branch
        head=$r.head
        decision=$decision
        command_hint=("git -C """ + $r.path + """ pull --ff-only")
      })
    }

    Ok ("JOBS_PATH=" + $JobsPath)
    Write-Host "ATLAS_SOFTWARE_FLEET_UPDATE_PLAN_OK" -ForegroundColor Green
  }

  "rollback-plan" {
    if(-not (Test-Path $InventoryPath)){ Die "RUN_INVENTORY_FIRST" }
    $inv = Get-Content -LiteralPath $InventoryPath -Raw | ConvertFrom-Json

    foreach($r in @($inv.repos)){
      Append-Ndjson $JobsPath ([ordered]@{
        schema="atlas.software_fleet.job.v1"
        utc=[DateTime]::UtcNow.ToString("O")
        job_type="rollback_plan"
        repo=$r.name
        path=$r.path
        branch=$r.branch
        rollback_to_head=$r.head
        command_hint=("git -C """ + $r.path + """ reset --hard " + $r.head)
      })
    }

    Ok ("JOBS_PATH=" + $JobsPath)
    Write-Host "ATLAS_SOFTWARE_FLEET_ROLLBACK_PLAN_OK" -ForegroundColor Green
  }

  "jobs" {
    if(Test-Path $JobsPath){
      Get-Content -LiteralPath $JobsPath | Select-Object -Last 40 | Out-Host
    }
    Ok ("JOBS_PATH=" + $JobsPath)
    Write-Host "ATLAS_SOFTWARE_FLEET_JOBS_OK" -ForegroundColor Green
  }
}

param([string]$RepoRoot = "C:\dev\atlas-update")
$ErrorActionPreference = "Continue"
Set-StrictMode -Version Latest

function Sha256HexBytes([byte[]]$b){
  $sha=[System.Security.Cryptography.SHA256]::Create()
  try { $h=$sha.ComputeHash($b) } finally { $sha.Dispose() }
  -join ($h | ForEach-Object { $_.ToString("x2") })
}
function Field($lines,$name){
  $m = @($lines | Where-Object { $_ -match ("^" + [regex]::Escape($name) + "=") } | Select-Object -First 1)
  if($m.Count -lt 1){ return $null }
  return ([string]$m[0]).Substring($name.Length + 1)
}

Set-Location $RepoRoot
$ts = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$run = Join-Path $RepoRoot ("proofs\audit\signed_green_" + $ts)
New-Item -ItemType Directory -Force -Path $run | Out-Null
$summary = Join-Path $run "summary.txt"
$results = [ordered]@{}
function Record($name,$ok,$note=""){ $results[$name] = @{ ok=$ok; note=$note }; ("CHECK " + $name + "=" + ($(if($ok){"PASS"}else{"FAIL"})) + $(if($note){" :: " + $note}else{""})) | Tee-Object -FilePath $summary -Append }

$cli = "src\tools\Atlas.HandoffCli\Atlas.HandoffCli.csproj"
"ATLAS TIER-0 SIGNED-GREEN RUN $ts" | Tee-Object -FilePath $summary
"RepoRoot=$RepoRoot" | Tee-Object -FilePath $summary -Append

# [1] CLEAN BUILD
Write-Host "=== [1] CLEAN BUILD ==="
dotnet build $cli -c Debug --nologo 2>&1 | Tee-Object -FilePath (Join-Path $run "build.txt")
Record "build" ($LASTEXITCODE -eq 0) ("exit=" + $LASTEXITCODE)

$packetDir = $null
if($results["build"].ok){
  # [2] EMIT
  Write-Host "=== [2] EMIT signed inventory packet ==="
  $emit = dotnet run --project $cli -c Debug --no-build -- emit-inventory --device dev-1 --hostname $env:COMPUTERNAME --os-family windows --os-version ([Environment]::OSVersion.Version.ToString()) --agent atlas-cli-dev --strength evidence --tag standalone 2>&1
  $emit | Tee-Object -FilePath (Join-Path $run "emit.txt")
  $emitOk = ($LASTEXITCODE -eq 0) -and (@($emit | Where-Object { $_ -eq "EMIT_OK" }).Count -ge 1)
  $pidHex = Field $emit "PACKET_ID"
  if($pidHex){ $pidHex = $pidHex -replace '^sha256:','' }
  Record "emit" $emitOk ("packet=" + $pidHex)
  if($emitOk -and $pidHex){ $packetDir = Join-Path $RepoRoot ("data\outbox\" + $pidHex) }
}

# [3] VERIFY (constitution + signature + blob)
if($packetDir){
  Write-Host "=== [3] VERIFY signed packet ==="
  & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot "scripts\_VERIFY_atlas_signed_inventory_packet_v1.ps1") -RepoRoot $RepoRoot -PacketDir $packetDir 2>&1 | Tee-Object -FilePath (Join-Path $run "verify.txt")
  $vtxt = Get-Content -Raw (Join-Path $run "verify.txt")
  $vok = ($vtxt -match "ATLAS_SIGNED_PACKET_VERIFY_OK") -and ($vtxt -match "SIGNATURE_VALID")
  Record "verify_signed_packet" $vok ("token=" + $vok)
} else { Record "verify_signed_packet" $false "no packet produced" }

# [4] DETERMINISM
if($results["build"].ok){
  Write-Host "=== [4] DETERMINISM ==="
  $fixed = "2020-01-01T00:00:00.0000000Z"
  $d1 = dotnet run --project $cli -c Debug --no-build -- emit-inventory --device det-1 --hostname DETHOST --os-family windows --os-version 10.0 --agent atlas-cli-dev --strength deterministic --tag det --captured-utc $fixed 2>&1
  $d2 = dotnet run --project $cli -c Debug --no-build -- emit-inventory --device det-1 --hostname DETHOST --os-family windows --os-version 10.0 --agent atlas-cli-dev --strength deterministic --tag det --captured-utc $fixed 2>&1
  ($d1 + "----" + $d2) | Tee-Object -FilePath (Join-Path $run "determinism.txt")
  $c1 = Field $d1 "COMMIT_HASH"; $c2 = Field $d2 "COMMIT_HASH"
  $r1 = Field $d1 "CONTENT_REF"; $r2 = Field $d2 "CONTENT_REF"
  $detOk = ($c1) -and ($c1 -eq $c2) -and ($r1) -and ($r1 -eq $r2)
  Record "determinism" $detOk ("commit1=" + $c1 + " commit2=" + $c2)
}

# [5] OFFLINE
if($env:ATLAS_NFL_URL){ Record "offline_local" $true ("NOTE ATLAS_NFL_URL set=" + $env:ATLAS_NFL_URL) }
else { Record "offline_local" $true "no ATLAS_NFL_URL; packet produced to local outbox" }

# [6] PLEDGE LEDGER LINKAGE (read-only)
$log = Join-Path $RepoRoot "data\pledge\pledge.ndjson"
if(Test-Path -LiteralPath $log -PathType Leaf){
  $llines = @(Get-Content -LiteralPath $log | Where-Object { $_.Trim() -ne "" })
  $prev = "sha256:" + ("0"*64); $seq = 0; $chainOk = $true; $break=""
  foreach($l in $llines){
    $o = $l | ConvertFrom-Json
    if([string]$o.local_prev_log_hash_sha256 -ne $prev){ $chainOk=$false; $break="prev-link@seq"+$o.local_seq; break }
    if([long]$o.local_seq -ne ($seq+1)){ $chainOk=$false; $break="seq@"+$o.local_seq; break }
    $seq = [long]$o.local_seq; $prev = [string]$o.local_log_hash_sha256
  }
  Record "pledge_chain_linkage" $chainOk ("entries=" + $llines.Count + $(if($break){" break="+$break}else{""}))
} else { Record "pledge_chain_linkage" $false "no pledge.ndjson" }


# [7] PLEDGE VERIFY (recompute hash-chain with the real canonicalizer)
if($results["build"].ok){
  Write-Host "=== [7] PLEDGE VERIFY (positive) ==="
  $pv = dotnet run --project $cli -c Debug --no-build -- verify-pledge 2>&1
  $pv | Tee-Object -FilePath (Join-Path $run "pledge_verify.txt")
  Record "verify_pledge" (($LASTEXITCODE -eq 0) -and (@($pv | Where-Object {$_ -eq "PLEDGE_VERIFY_OK"}).Count -ge 1)) ""
}

# [8] LEDGER TAMPER (must be detected)
if($results["build"].ok){
  Write-Host "=== [8] LEDGER TAMPER (negative) ==="
  $src = Join-Path $RepoRoot "data\pledge\pledge.ndjson"
  $tam = Join-Path $run "pledge.tampered.ndjson"
  $lines = @(Get-Content -LiteralPath $src)
  if($lines.Count -ge 1){
    $lines[0] = ([regex]'("commit_hash":"sha256:)([0-9a-f])').Replace([string]$lines[0], { param($m) $m.Groups[1].Value + $(if($m.Groups[2].Value -eq 'a'){'b'}else{'a'}) })
  }
  [System.IO.File]::WriteAllLines($tam, $lines, (New-Object System.Text.UTF8Encoding($false)))
  $tv = dotnet run --project $cli -c Debug --no-build -- verify-pledge --log $tam 2>&1
  $tv | Tee-Object -FilePath (Join-Path $run "pledge_tamper.txt")
  $detected = ($LASTEXITCODE -ne 0) -and (@($tv | Where-Object {$_ -match "PLEDGE_VERIFY_OK"}).Count -eq 0)
  Record "ledger_tamper_detected" $detected ("exit=" + $LASTEXITCODE)
}

# [9] NEGATIVE VECTORS
if($packetDir){
  Write-Host "=== [9] NEGATIVE VECTORS ==="
  $negOut = Join-Path $run "negatives"
  & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_negative_vectors_signed_v1.ps1") -RepoRoot $RepoRoot -SrcPacketDir $packetDir -OutDir $negOut 2>&1 | Tee-Object -FilePath (Join-Path $run "negatives.txt")
  $negtxt = Get-Content -Raw (Join-Path $run "negatives.txt")
  Record "negative_vectors" ([bool]($negtxt -match "ATLAS_NEGATIVE_VECTORS_OK")) ""
}


# [10] GOLDEN VECTOR (pinned canonical determinism)
if($results["build"].ok){
  Write-Host "=== [10] GOLDEN VECTOR ==="
  $GOLD_CONTENT="sha256:fbcbc7044a1ef6c8b2c3d2eaf72f1c25390b2ad46d0adc2b91adff2e4cec00f7"
  $g = dotnet run --project $cli -c Debug --no-build -- emit-inventory --device det-1 --hostname DETHOST --os-family windows --os-version 10.0 --agent atlas-cli-dev --strength deterministic --tag det --captured-utc "2020-01-01T00:00:00.0000000Z" 2>&1
  $g | Tee-Object -FilePath (Join-Path $run "golden.txt")
  $gr=Field $g "CONTENT_REF"; $gc=Field $g "COMMIT_HASH"
  Record "golden_vector" ($gr -eq $GOLD_CONTENT) ("content=" + $gr + " commit=" + $gc)
}

# ---- verdict ----
$critical = @("build","emit","verify_signed_packet","determinism","verify_pledge","ledger_tamper_detected","negative_vectors","golden_vector")
$allCritical = $true
foreach($c in $critical){ if(-not ($results.Contains($c) -and $results[$c].ok)){ $allCritical=$false } }
"" | Tee-Object -FilePath $summary -Append
if($allCritical){
  "ATLAS_TIER0_FULL_GREEN_OK" | Tee-Object -FilePath $summary -Append
  Write-Host "ATLAS_TIER0_FULL_GREEN_OK" -ForegroundColor Green
  if($packetDir){
    Write-Host "=== [11] FREEZE ==="
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot "scripts\_RUN_atlas_tier0_freeze_v1.ps1") -RepoRoot $RepoRoot -RunDir $run -PacketDir $packetDir 2>&1 | Tee-Object -FilePath (Join-Path $run "freeze.txt")
  }
} else {
  "ATLAS_TIER0_FULL_GREEN_FAIL" | Tee-Object -FilePath $summary -Append
  Write-Host "ATLAS_TIER0_FULL_GREEN_FAIL" -ForegroundColor Red
}
"RUN_DIR=$run" | Tee-Object -FilePath $summary -Append
Write-Host ("RUN_DIR=" + $run)

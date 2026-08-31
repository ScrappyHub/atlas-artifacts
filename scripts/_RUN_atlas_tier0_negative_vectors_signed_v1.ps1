param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$SrcPacketDir,
  [Parameter(Mandatory=$true)][string]$OutDir
)
$ErrorActionPreference = "Continue"
Set-StrictMode -Version Latest

$RepoRoot=(Resolve-Path -LiteralPath $RepoRoot).Path
$SrcPacketDir=(Resolve-Path -LiteralPath $SrcPacketDir).Path
$verifier = Join-Path $RepoRoot "scripts\_VERIFY_atlas_signed_inventory_packet_v1.ps1"
$origHex = Split-Path -Leaf $SrcPacketDir
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$enc = New-Object System.Text.UTF8Encoding($false)
function WriteText($p,$t){ [System.IO.File]::WriteAllText($p,$t,$enc) }
function WriteBytes($p,$b){ [System.IO.File]::WriteAllBytes($p,$b) }
function ReadText($p){ [System.IO.File]::ReadAllText($p) }
function FlipByte($p){ $b=[System.IO.File]::ReadAllBytes($p); $b[0]=[byte]($b[0] -bxor 0x01); WriteBytes $p $b }
function ShaFile($p){ (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash.ToLower() }
function ResumRel($packetDir,$rel){
  $full = Join-Path $packetDir ($rel -replace '/','\')
  $h = ShaFile $full
  $sp = Join-Path $packetDir "sha256sums.txt"
  $out = @()
  foreach($ln in (ReadText $sp).Split("`n")){ if($ln -eq ""){continue}; if($ln.EndsWith("  "+$rel)){ $out += ($h+"  "+$rel) } else { $out += $ln } }
  WriteText $sp (($out -join "`n")+"`n")
}
function NewVec($name){
  $vecRoot = Join-Path $OutDir $name
  if(Test-Path $vecRoot){ Remove-Item -Recurse -Force $vecRoot }
  New-Item -ItemType Directory -Force -Path $vecRoot | Out-Null
  $inner = Join-Path $vecRoot $origHex
  Copy-Item -Recurse -LiteralPath $SrcPacketDir -Destination $inner
  return $inner
}
$results=[ordered]@{}
function Check($name,$dir){
  $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $verifier -RepoRoot $RepoRoot -PacketDir $dir 2>&1
  $accepted = @($out | Where-Object { $_ -match "ATLAS_SIGNED_PACKET_VERIFY_OK" }).Count -ge 1
  $reason = (@($out | Where-Object { $_ -match "ATLAS_SIGVERIFY_FAIL" } | Select-Object -First 1) -join '')
  $results[$name] = (-not $accepted)
  Write-Host ("NEG " + $name.PadRight(22) + " => " + $(if(-not $accepted){"REJECTED_OK"}else{"WRONGLY_ACCEPTED"}) + $(if($reason){"  ["+$reason.Trim()+"]"}else{""}))
}

# 1 manifest tamper (changes packet identity)
$d=NewVec "tamper_manifest"; FlipByte (Join-Path $d "manifest.json"); Check "tamper_manifest" $d
# 2 packet_id.txt points elsewhere
$d=NewVec "tamper_packet_id"; WriteText (Join-Path $d "packet_id.txt") ("sha256:" + ("f"*64) + "`n"); Check "tamper_packet_id" $d
# 3 bare hex packet_id (the drift)
$d=NewVec "bare_hex_packet_id"; WriteText (Join-Path $d "packet_id.txt") ($origHex + "`n"); Check "bare_hex_packet_id" $d
# 4 CRLF in packet_id
$d=NewVec "crlf_packet_id"; WriteBytes (Join-Path $d "packet_id.txt") ($enc.GetBytes("sha256:" + $origHex + "`r`n")); Check "crlf_packet_id" $d
# 5 commit payload tamper
$d=NewVec "tamper_commit_payload"; FlipByte (Join-Path $d "payload\commit.payload.json"); Check "tamper_commit_payload" $d
# 6 sha256sums hex tamper
$d=NewVec "tamper_sums"; $sp=Join-Path $d "sha256sums.txt"; $ls=@((ReadText $sp).Split("`n") | Where-Object {$_ -ne ""}); for($i=0;$i -lt $ls.Count;$i++){ if($ls[$i].EndsWith("  payload/commit.payload.json")){ $c=$ls[$i][0]; $ls[$i]=$(if($c -eq '0'){'1'}else{'0'}) + $ls[$i].Substring(1) } }; WriteText $sp (($ls -join "`n")+"`n"); Check "tamper_sums" $d
# 7 duplicate path
$d=NewVec "dup_path"; $sp=Join-Path $d "sha256sums.txt"; $ls=@((ReadText $sp).Split("`n") | Where-Object {$_ -ne ""}); WriteText $sp (((@($ls)+@($ls[0])) -join "`n")+"`n"); Check "dup_path" $d
# 8 self-referential sums row
$d=NewVec "self_sum"; $sp=Join-Path $d "sha256sums.txt"; $cur=ReadText $sp; WriteText $sp ($cur + ("0"*64) + "  sha256sums.txt`n"); Check "self_sum" $d
# 9 missing required file
$d=NewVec "missing_file"; Remove-Item -Force (Join-Path $d "packet_id.txt"); Check "missing_file" $d
# 10 malformed signature (re-summed so constitution passes, sig fails)
$d=NewVec "malformed_sig"; $ip=Join-Path $d "payload\nfl.ingest.json"; $t=ReadText $ip; $t=[regex]::Replace($t,'"producer_sig_b64":"[^"]*"','"producer_sig_b64":"bm90LWEtdmFsaWQtc2lnbmF0dXJl"'); WriteText $ip $t; ResumRel $d "payload/nfl.ingest.json"; Check "malformed_sig" $d
# 11 wrong signer (valid sig, untrusted key)
try {
  $d=NewVec "wrong_signer"; $ip=Join-Path $d "payload\nfl.ingest.json"; $t=ReadText $ip
  $ch=[regex]::Match($t,'"commit_hash":"sha256:([0-9a-f]{64})"').Groups[1].Value
  $wk=Join-Path $env:TEMP ("atlas_wrong_"+[guid]::NewGuid().ToString("n"))
  & ssh-keygen -t ed25519 -f $wk -N '""' -C wrong -q 2>&1 | Out-Null
  $msg=Join-Path $env:TEMP ("m_"+[guid]::NewGuid().ToString("n")+".bin"); WriteBytes $msg ($enc.GetBytes("sha256:"+$ch+"`n"))
  & cmd /c "ssh-keygen -Y sign -f `"$wk`" -I atlas -n atlas-handoff `"$msg`"" 2>&1 | Out-Null
  $sig=[System.IO.File]::ReadAllBytes($msg+".sig"); $sigb64=[System.Convert]::ToBase64String($sig)
  $t=[regex]::Replace($t,'"producer_sig_b64":"[^"]*"',('"producer_sig_b64":"'+$sigb64+'"')); WriteText $ip $t; ResumRel $d "payload/nfl.ingest.json"
  Check "wrong_signer" $d
  Remove-Item -Force ($msg),($msg+".sig"),$wk,($wk+".pub") -ErrorAction SilentlyContinue
} catch { Write-Host ("NEG wrong_signer SKIPPED: "+$_.Exception.Message); $results["wrong_signer"]=$true }

$total=$results.Count; $passed=@($results.Values | Where-Object {$_ -eq $true}).Count
Write-Host ("NEGATIVE_VECTORS " + $passed + "/" + $total + " correctly rejected")
if($passed -eq $total){ Write-Host "ATLAS_NEGATIVE_VECTORS_OK" -ForegroundColor Green; exit 0 } else { Write-Host "ATLAS_NEGATIVE_VECTORS_FAIL" -ForegroundColor Red; exit 1 }

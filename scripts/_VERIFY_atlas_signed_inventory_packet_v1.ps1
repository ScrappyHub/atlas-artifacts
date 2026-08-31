param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$PacketDir
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Die([string]$m){ throw ("ATLAS_SIGVERIFY_FAIL: " + $m) }
function Ok([string]$m){ Write-Host ("OK: " + $m) -ForegroundColor Green }

function Sha256HexBytes([byte[]]$b){
  $sha=[System.Security.Cryptography.SHA256]::Create()
  try { $h=$sha.ComputeHash($b) } finally { $sha.Dispose() }
  -join ($h | ForEach-Object { $_.ToString("x2") })
}
function ReadBytes([string]$p){
  if(-not (Test-Path -LiteralPath $p -PathType Leaf)){ Die ("MISSING_FILE: " + $p) }
  [System.IO.File]::ReadAllBytes($p)
}
function AssertNoCR([byte[]]$b,[string]$label){ if($b -contains 13){ Die ("CR_NOT_ALLOWED_LF_ONLY: " + $label) } }

$RepoRoot=(Resolve-Path -LiteralPath $RepoRoot).Path
$PacketDir=(Resolve-Path -LiteralPath $PacketDir).Path
$leaf=Split-Path -Leaf $PacketDir

# ---- Written Packet Constitution v1 Option A ----
$manB=ReadBytes (Join-Path $PacketDir "manifest.json")
$pidB=ReadBytes (Join-Path $PacketDir "packet_id.txt")
$sumB=ReadBytes (Join-Path $PacketDir "sha256sums.txt")
AssertNoCR $pidB "packet_id.txt"
AssertNoCR $sumB "sha256sums.txt"

$pidHex=Sha256HexBytes $manB
if($leaf.ToLowerInvariant() -ne $pidHex){ Die ("DIRNAME_MISMATCH leaf=" + $leaf + " expect=" + $pidHex) }

$pidText=[System.Text.Encoding]::UTF8.GetString($pidB)
if($pidText -ne ("sha256:" + $pidHex + "`n")){ Die ("PACKET_ID_TXT_MISMATCH got=" + $pidText.TrimEnd()) }

$manText=[System.Text.Encoding]::UTF8.GetString($manB)
if($manText.IndexOf('"packet_id"',[System.StringComparison]::OrdinalIgnoreCase) -ge 0){ Die "MANIFEST_CONTAINS_PACKET_ID_FORBIDDEN" }

$sumText=[System.Text.Encoding]::UTF8.GetString($sumB)
if(-not $sumText.EndsWith("`n")){ Die "SHA256SUMS_MUST_END_WITH_LF" }
$lines = @($sumText.Split("`n") | Where-Object { $_ -ne "" })
$map=@{}
foreach($ln in $lines){
  $idx=$ln.IndexOf("  ")
  if($idx -lt 1){ Die ("SUMS_BAD_LINE_NO_DOUBLESPACE: " + $ln) }
  $hex=$ln.Substring(0,$idx).Trim()
  $rel=$ln.Substring($idx+2).Trim().Replace("\","/")
  if($hex.Length -ne 64){ Die ("SUMS_BAD_HEXLEN: " + $ln) }
  if($map.ContainsKey($rel)){ Die ("SUMS_DUP_PATH: " + $rel) }
  if($rel -eq "sha256sums.txt"){ Die "SUMS_SELF_REFERENCE_FORBIDDEN" }
  $map[$rel]=$hex.ToLowerInvariant()
}
foreach($req in @("manifest.json","packet_id.txt","payload/nfl.ingest.json","payload/commit.payload.json")){
  if(-not $map.ContainsKey($req)){ Die ("SUMS_MISSING_REQUIRED: " + $req) }
}
foreach($rel in @($map.Keys)){
  $fp=Join-Path $PacketDir ($rel.Replace("/",[System.IO.Path]::DirectorySeparatorChar))
  $got=Sha256HexBytes (ReadBytes $fp)
  if($got -ne $map[$rel]){ Die ("HASH_MISMATCH: " + $rel + " got=" + $got + " want=" + $map[$rel]) }
}
if($map["manifest.json"] -ne $pidHex){ Die "MANIFEST_SUM_NE_PACKETID" }
Ok ("CONSTITUTION_OK packet=sha256:" + $pidHex + " files=" + $map.Count)

# ---- Cryptographic commitment + signature ----
$commitBytes=ReadBytes (Join-Path $PacketDir "payload/commit.payload.json")
$commitSha=Sha256HexBytes $commitBytes
$ingestBytes=ReadBytes (Join-Path $PacketDir "payload/nfl.ingest.json")
$ingest=[System.Text.Encoding]::UTF8.GetString($ingestBytes) | ConvertFrom-Json

$commitHashField=[string]$ingest.commit_hash
if($commitHashField -ne ("sha256:" + $commitSha)){ Die ("COMMIT_HASH_NE_CANONICAL got=" + $commitHashField + " expect=sha256:" + $commitSha) }

if([string]$ingest.payload_mode -eq "plaintext"){
  $decoded=[System.Convert]::FromBase64String([string]$ingest.payload_b64)
  if((Sha256HexBytes $decoded) -ne $commitSha){ Die "PAYLOAD_B64_NE_COMMIT" }
}

$commit=[System.Text.Encoding]::UTF8.GetString($commitBytes) | ConvertFrom-Json
$cref=[string]$commit.content_ref
if($cref -notmatch '^sha256:[0-9a-f]{64}$'){ Die ("CONTENT_REF_BAD: " + $cref) }
$crefHex=$cref.Substring(7)
$blob=Join-Path $RepoRoot ("data\blobs\" + $crefHex)
if(-not (Test-Path -LiteralPath $blob -PathType Leaf)){ Die ("BLOB_MISSING: " + $blob) }
if((Sha256HexBytes (ReadBytes $blob)) -ne $crefHex){ Die "BLOB_HASH_MISMATCH" }
Ok ("CONTENT_REF_RESOLVES " + $cref)

$sigB64=[string]$ingest.producer_sig_b64
$keyId=[string]$ingest.producer_key_id
$pub=(Get-Content -Raw (Join-Path $RepoRoot "keys\atlas-dev-ed25519.pub")).Trim()
$expKeyId="sha256:" + (Sha256HexBytes ([System.Text.Encoding]::UTF8.GetBytes($pub + "`n")))
if($keyId -ne $expKeyId){ Die ("KEY_ID_MISMATCH got=" + $keyId + " expect=" + $expKeyId) }

$tmp=Join-Path ([System.IO.Path]::GetTempPath()) ("atlas_sv_" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
  $af=Join-Path $tmp "allowed_signers"
  [System.IO.File]::WriteAllText($af, ("atlas " + $pub + "`n"), (New-Object System.Text.UTF8Encoding($false)))
  $sf=Join-Path $tmp "sig.sig"
  [System.IO.File]::WriteAllBytes($sf, [System.Convert]::FromBase64String($sigB64))
  $mf=Join-Path $tmp "msg.bin"
  [System.IO.File]::WriteAllBytes($mf, [System.Text.Encoding]::UTF8.GetBytes("sha256:" + $commitSha + "`n"))

  $cmdline = "ssh-keygen -Y verify -f `"$af`" -I atlas -n atlas-handoff -s `"$sf`" < `"$mf`""
  $out = & cmd /c $cmdline 2>&1
  $code = $LASTEXITCODE
  if($code -ne 0){ Die ("SIGNATURE_INVALID exit=" + $code + " out=" + ($out -join ' ')) }
  Ok ("SIGNATURE_VALID principal=atlas ns=atlas-handoff key=" + $keyId)
} finally {
  try { Remove-Item -Recurse -Force -LiteralPath $tmp -ErrorAction Stop } catch { Write-Host ("CLEANUP_SKIP: " + $_.Exception.Message) }
}

Write-Host "ATLAS_SIGNED_PACKET_VERIFY_OK" -ForegroundColor Green
Write-Host ("PACKET_ID=sha256:" + $pidHex)
Write-Host ("COMMIT_HASH=sha256:" + $commitSha)
Write-Host ("CONTENT_REF=" + $cref)

exit 0

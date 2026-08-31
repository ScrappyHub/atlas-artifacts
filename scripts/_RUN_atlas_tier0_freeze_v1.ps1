param(
  [Parameter(Mandatory=$true)][string]$RepoRoot,
  [Parameter(Mandatory=$true)][string]$RunDir,
  [Parameter(Mandatory=$true)][string]$PacketDir
)
$ErrorActionPreference="Continue"; Set-StrictMode -Version Latest
$RepoRoot=(Resolve-Path -LiteralPath $RepoRoot).Path
$PacketDir=(Resolve-Path -LiteralPath $PacketDir).Path
$enc=New-Object System.Text.UTF8Encoding($false)
function ShaFile($p){ (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash.ToLower() }
$ts=(Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$fz=Join-Path $RepoRoot ("proofs\freeze\atlas_tier0_"+$ts)
New-Item -ItemType Directory -Force -Path $fz | Out-Null
Copy-Item -Recurse -LiteralPath $PacketDir -Destination (Join-Path $fz "packet")
$commitFile=Join-Path $PacketDir "payload\commit.payload.json"
$commitHash="sha256:"+(ShaFile $commitFile)
$commitObj=(Get-Content -Raw -LiteralPath $commitFile | ConvertFrom-Json)
$contentRef=[string]$commitObj.content_ref
$packetId="sha256:"+(Split-Path -Leaf $PacketDir)
$lastRun=Join-Path $RepoRoot "proofs\audit\LAST_RUN.txt"
if(Test-Path -LiteralPath $lastRun){ Copy-Item -LiteralPath $lastRun -Destination (Join-Path $fz "full_green_transcript.txt") }
$tracked=@(
 "src\tools\Atlas.HandoffCli\Program.cs","src\shared\Atlas.Handoff\HandoffEngine.cs",
 "src\shared\Atlas.Handoff\CanonicalJson.cs","src\shared\Atlas.Handoff\SshKeygenSigner.cs",
 "src\shared\Atlas.Handoff\BlobStore.cs","src\shared\Atlas.Handoff\Models.cs",
 "src\shared\Atlas.Handoff\AtlasInventoryPayloads.cs","src\shared\Atlas.Handoff\Hashing.cs",
 "src\shared\Atlas.Handoff\ContentRef.cs","scripts\_VERIFY_atlas_signed_inventory_packet_v1.ps1",
 "scripts\_RUN_atlas_tier0_signed_green_v1.ps1","scripts\_RUN_atlas_tier0_negative_vectors_signed_v1.ps1",
 "keys\atlas-dev-ed25519.pub")
$tf=@()
foreach($rel in $tracked){ $full=Join-Path $RepoRoot $rel; if(Test-Path -LiteralPath $full){ $tf+=[PSCustomObject]@{ path=$rel; sha256=(ShaFile $full) } } }
$receipt=[PSCustomObject]@{
  schema="atlas.tier0.freeze.v1"; utc=$ts; repo_root=$RepoRoot;
  packet_id=$packetId; commit_hash=$commitHash; content_ref=$contentRef;
  golden_content_ref="sha256:fbcbc7044a1ef6c8b2c3d2eaf72f1c25390b2ad46d0adc2b91adff2e4cec00f7";
  producer_key_id="sha256:b9a6d38c2debe88ee4bd5dd5a500351277c181d286e91ab690f71307daf6279a";
  full_green_token="ATLAS_TIER0_FULL_GREEN_OK"; tracked_files=$tf }
[System.IO.File]::WriteAllText((Join-Path $fz "freeze_receipt.json"), ($receipt | ConvertTo-Json -Depth 6), $enc)
$files=Get-ChildItem -Recurse -File -LiteralPath $fz | Where-Object { $_.Name -ne "sha256sums.txt" }
$sums=@()
foreach($f in ($files | Sort-Object FullName)){ $rel=$f.FullName.Substring($fz.Length+1).Replace("\","/"); $sums+=((ShaFile $f.FullName)+"  "+$rel) }
[System.IO.File]::WriteAllText((Join-Path $fz "sha256sums.txt"), (($sums -join "`n")+"`n"), $enc)
Write-Host "ATLAS_TIER0_FREEZE_OK"
Write-Host ("FREEZE_DIR="+$fz)

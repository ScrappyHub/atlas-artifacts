# SPEC — Atlas Tier-0 Artifacts v1

## What this project is
Atlas Tier-0 is a standalone deterministic artifact instrument for emitting canonical inventory evidence as content-addressed blobs and packaging those blobs into verifiable packet directories with append-only receipts.

## Tier-0 scope
- Canonical inventory snapshot emission
- Content-addressed blob storage
- Blob verification by content reference
- Deterministic inventory packet emission
- Deterministic packet verification
- Full selftest runner
- Negative vectors
- Full-green evidence bundle and frozen latest-green packet

## Non-scope at Tier-0
- Full update orchestration service
- Snapshot/restore engine ownership
- TRIAD restore execution ownership
- Broad service inventory UX
- Rich job/history browsing surface
- Remote orchestration or fleet management

## Boundaries
Atlas emits and verifies artifact evidence.
TRIAD owns snapshot/restore substrate.
Legacy Doctor may later consume Atlas/Triad capabilities in a broader service surface.
Atlas Tier-0 must stand alone before wider integration.

## Canonical environment
- Windows PowerShell 5.1
- Set-StrictMode -Version Latest
- `$ErrorActionPreference = "Stop"`
- Deterministic write-to-disk, parse-gate, run via `powershell.exe -File`
- UTF-8 without BOM
- LF line endings
- SHA-256 for identity

## Core objects
### Blob
A blob is stored under:
`data\blobs\<sha256hex>`

Identity law:
`content_ref = "sha256:" + sha256(blob bytes)`

### Inventory snapshot
Current emitted schema:
`atlas.inventory.snapshot.v1`

### Inventory packet
Current packet contains:
- `payload\content_ref.txt`
- `payload\inventory.snapshot.meta.json`
- `manifest.json`
- `packet_id.txt`
- `sha256sums.txt`

### PacketId
Current Tier-0 law:
`PacketId = sha256(manifest.json on-disk bytes)`

`packet_id.txt` contains the PacketId text and ends with LF.

### Pledge receipt
Append-only local receipt path:
`data\pledges.ndjson`

## CLI surface
### emit-inventory
Emits a canonical inventory snapshot blob and returns:
- `CONTENT_REF=...`
- `BLOB_PATH=...`
- `CAPTURED_UTC=...`
- `DEVICE_ID=...`

### verify-blob
Verifies a blob exists for a given content reference and returns:
- `VERIFY_BLOB_OK`
- `CONTENT_REF=...`
- `BLOB_PATH=...`
- `BLOB_LEN=...`

## Runner surface
### `_RUN_atlas_tier0_smoke_emit_and_verify_blob_v1.ps1`
Builds confidence in emit + verify blob path.

### `_RUN_atlas_emit_inventory_packet_v1.ps1`
Emits a packet directory and local pledge line.

### `_RUN_atlas_tier0_packet_verify_v1.ps1`
Verifies packet deterministically, including:
- packet_id.txt matches manifest hash
- referenced blob exists
- sha256sums rows match payload files
- sha256sums self-row is correct

### `_RUN_atlas_tier0_full_selftest_v1.ps1`
Runs build + blob smoke + packet emit + packet verify.

### `_RUN_atlas_tier0_negative_vectors_v2.ps1`
Runs negative vectors:
- missing blob
- tampered manifest
- tampered packet_id
- tampered sha256sums

### `_RUN_atlas_full_green_v1.ps1`
Runs full selftest and negative vectors, emits evidence bundle, freezes latest-green packet, and appends a receipt.

## Deterministic laws
1. All text artifacts are UTF-8 no BOM + LF.
2. Blob identity is SHA-256 of raw blob bytes.
3. PacketId equals SHA-256 of `manifest.json` on-disk bytes.
4. `packet_id.txt` stores PacketId text with LF.
5. `sha256sums.txt` rows are formatted:
   `<sha256>  <relative-path>`
6. `sha256sums.txt` includes a self-row for `sha256sums.txt`.
7. Verification is non-mutating.

## Proof artifacts
- `proofs\receipts\atlas.ndjson`
- `proofs\receipts\atlas_tier0_full_green\<timestamp>\`
- `test_vectors\atlas_tier0\frozen_latest_green\`

## Definition of Done — Tier-0
Atlas Tier-0 is done when a clean machine can:
- build shared + CLI surfaces
- emit an inventory blob
- verify a blob by content reference
- emit a deterministic inventory packet
- verify that packet non-mutatingly
- pass full selftest
- pass negative vectors
- emit deterministic evidence bundle
- freeze a latest-green packet/vector surface
- append receipt lines deterministically

## Post-Tier-0 next slices
1. Deterministic inventory history
2. Deterministic job ledger
3. TRIAD-facing integration contract

# SPEC — Atlas Tier-0 Artifacts v1

## What this project is to spec

Atlas Artifacts is the deterministic artifact/update instrument that emits local inventory evidence into content-addressed blobs, packages deterministic inventory packets, verifies those packets non-mutatingly, records append-only inventory history and job-ledger lines, and freezes reproducible latest-green evidence suitable for later integration with TRIAD as the restore substrate.

Atlas is **not** the restore substrate itself. TRIAD remains the canonical snapshot/restore measurement and restore layer. Atlas is the updater / artifact orchestrator that will later consume TRIAD-facing references and job boundaries.

## Instrument environment

- Windows PowerShell 5.1
- StrictMode Latest
- `$ErrorActionPreference = 'Stop'`
- Deterministic workflow: write to disk -> parse-gate -> run via child `powershell.exe -File`
- Text discipline: UTF-8 no BOM, LF line endings
- Hash discipline: SHA-256, deterministic ordering
- Packet discipline: manifest hash = packet id for current Tier-0 Atlas packet surface
- Verification must be non-mutating

## Current Tier-0 product surface

### Blob / inventory evidence
- `src/shared/Atlas.Handoff/*`
- `src/tools/Atlas.HandoffCli/*`
- `scripts/_RUN_atlas_emit_inventory_smoke_v1.ps1`
- `scripts/_RUN_atlas_tier0_smoke_emit_and_verify_blob_v1.ps1`

### Packet surface
- `scripts/_RUN_atlas_emit_inventory_packet_v1.ps1`
- `scripts/_RUN_atlas_tier0_packet_verify_v1.ps1`

### Selftests / vectors / freeze
- `scripts/_RUN_atlas_tier0_full_selftest_v1.ps1`
- `scripts/_RUN_atlas_tier0_negative_vectors_v2.ps1`
- `scripts/_RUN_atlas_full_green_v1.ps1`
- `test_vectors/atlas_tier0/frozen_latest_green/*`

### History / job ledger surface
- `scripts/_lib_atlas_history_jobs_v1.ps1`
- `scripts/_RUN_atlas_inventory_history_record_v1.ps1`
- `scripts/_RUN_atlas_job_ledger_record_v1.ps1`
- `scripts/_RUN_atlas_inventory_history_and_jobs_smoke_v1.ps1`
- `data/inventory_history.ndjson`
- `data/jobs.ndjson`

## Canonical outputs

### Blob output
- `data/blobs/<sha256>`

### Packet output
- `data/outbox/<packet_id>/`
  - `manifest.json`
  - `packet_id.txt`
  - `sha256sums.txt`
  - `payload/content_ref.txt`
  - `payload/inventory.snapshot.meta.json`

### Receipts / evidence
- `proofs/receipts/atlas.ndjson`
- `proofs/receipts/atlas_tier0_full_green/<timestamp>/...`

### Frozen latest-green vector
- `test_vectors/atlas_tier0/frozen_latest_green/packet/*`
- `test_vectors/atlas_tier0/frozen_latest_green/FREEZE_MANIFEST.txt`
- `test_vectors/atlas_tier0/frozen_latest_green/CANONICAL_STATUS.md`

## Tier-0 invariants

1. Blob content reference must equal SHA-256 of emitted blob bytes.
2. Packet manifest hash must equal `packet_id.txt` content.
3. Packet directory name must equal packet id.
4. `sha256sums.txt` must validate all non-self rows and its reconstructed self row.
5. Packet verification is non-mutating.
6. Full selftest and negative vectors must both pass before full-green freeze is accepted.
7. Emit and verify surfaces each record deterministic job-ledger lines.
8. Inventory history is append-only evidence of emitted inventory captures.
9. Atlas remains updater/orchestrator; TRIAD remains restore substrate.

## Tier-0 definition of done

Atlas Tier-0 is GREEN when, on a clean machine, one deterministic command:
- parse-gates the product scripts,
- builds shared + CLI projects,
- emits and verifies an inventory blob,
- emits and verifies an inventory packet non-mutatingly,
- passes negative vectors with stable failure tokens,
- records append-only inventory history and job-ledger evidence,
- writes a full-green evidence bundle,
- refreshes frozen latest-green packet material,
- appends a receipt to `proofs/receipts/atlas.ndjson`,
- prints `ATLAS_TIER0_FULL_GREEN_OK`.

## Next boundary after Tier-0 freeze

The next slice is the TRIAD-facing integration boundary:
- define the TRIAD import/reference contract,
- define artifact/update job types for TRIAD-backed work,
- define snapshot reference objects,
- define restore-prep / update-prep handoff surfaces,
- keep Atlas as updater/orchestrator rather than restore substrate.

# WBS — Atlas Tier-0 Progress Ledger

## What this project is to spec

Atlas Artifacts is the deterministic artifact/update instrument for Constellation / Atlas Systems that emits local inventory evidence, packages deterministic inventory packets, verifies them non-mutatingly, records history and job evidence, and freezes latest-green reproducible artifact state. Atlas depends conceptually on TRIAD for the future restore substrate boundary, but Tier-0 Atlas must stand on its own current deterministic artifact surface.

## Status summary

Current Tier-0 status: **GREEN on frozen artifact/update surface**.

Latest authoritative green gate:
- `_RUN_atlas_full_green_v1.ps1`
- token: `ATLAS_TIER0_FULL_GREEN_OK`

## Completed slices

### 01. Shared library + CLI evidence emission
Status: [GREEN]
Notes:
- Shared handoff library builds
- CLI builds
- emit-inventory works
- verify-blob works

### 02. Blob smoke verification
Status: [GREEN]
Notes:
- smoke runner emits and verifies blob
- stable token:
  - `ATLAS_TIER0_SMOKE_EMIT_AND_VERIFY_BLOB_OK`

### 03. Packet emission
Status: [GREEN]
Notes:
- deterministic packet folder emitted
- packet directory keyed by packet id
- manifest / packet_id / sha256sums / payload surface stable

### 04. Packet verification
Status: [GREEN]
Notes:
- non-mutating verifier passes
- validates manifest hash, packet_id, payload file hashes, blob existence, sha256 self-row reconstruction
- stable token:
  - `ATLAS_TIER0_PACKET_VERIFY_OK`

### 05. Full selftest
Status: [GREEN]
Notes:
- builds shared + CLI
- runs blob smoke
- runs packet emit
- runs packet verify
- stable token:
  - `ATLAS_TIER0_FULL_SELFTEST_OK`

### 06. Negative vectors
Status: [GREEN]
Notes:
- missing blob
- tampered manifest
- tampered packet_id
- tampered sha256sums
- stable token:
  - `ATLAS_TIER0_NEGATIVE_VECTORS_OK`

### 07. Full-green freeze runner
Status: [GREEN]
Notes:
- re-runs required parse-gates
- rebuilds projects
- runs full selftest
- runs negative vectors
- writes evidence bundle
- refreshes frozen latest-green vector
- appends atlas receipt
- stable token:
  - `ATLAS_TIER0_FULL_GREEN_OK`

### 08. Inventory history surface
Status: [GREEN]
Notes:
- append-only inventory history recording exists
- history smoke passes

### 09. Job ledger surface
Status: [GREEN]
Notes:
- append-only job ledger recording exists
- emit packet writes job id
- verify packet writes job id
- history/job smoke passes

## Current intentional Tier-0 product surface

- `src/shared/Atlas.Handoff/*`
- `src/tools/Atlas.HandoffCli/*`
- `scripts/_RUN_atlas_emit_inventory_smoke_v1.ps1`
- `scripts/_RUN_atlas_tier0_smoke_emit_and_verify_blob_v1.ps1`
- `scripts/_RUN_atlas_emit_inventory_packet_v1.ps1`
- `scripts/_RUN_atlas_tier0_packet_verify_v1.ps1`
- `scripts/_RUN_atlas_tier0_full_selftest_v1.ps1`
- `scripts/_RUN_atlas_tier0_negative_vectors_v2.ps1`
- `scripts/_RUN_atlas_full_green_v1.ps1`
- `scripts/_lib_atlas_history_jobs_v1.ps1`
- `scripts/_RUN_atlas_inventory_history_record_v1.ps1`
- `scripts/_RUN_atlas_job_ledger_record_v1.ps1`
- `scripts/_RUN_atlas_inventory_history_and_jobs_smoke_v1.ps1`
- `docs/SPEC_atlas_tier0_artifacts_v1.md`
- `docs/WBS_ATLAS_TIER0_PROGRESS_LEDGER.md`
- `test_vectors/atlas_tier0/frozen_latest_green/*`
- `proofs/receipts/atlas.ndjson`

## Not part of current Tier-0 completion claim

These are future / adjacent slices and are **not** required to claim current Tier-0 green:
- snapshot capture / restore substrate
- broader Atlas service expansion
- Legacy Doctor wider service boundary
- full TRIAD restore integration
- scheduler / distributed workers
- richer job orchestration beyond current append-only ledger

## Next locked slice

### 10. TRIAD-facing integration boundary
Status: [YELLOW]
Depends:
- current Tier-0 freeze
Notes:
- define TRIAD import/reference contract
- define update job types
- define snapshot reference schema
- define restore-prep / update-prep handoff rules
- Atlas remains updater/orchestrator; TRIAD remains restore substrate

## Recommended next implementation order

1. Freeze and commit the expanded current green surface.
2. Add explicit TRIAD-facing boundary docs and schemas.
3. Add deterministic runner(s) for TRIAD-reference intake.
4. Add artifact/update job taxonomy for TRIAD-backed work.
5. Only then expand service breadth.

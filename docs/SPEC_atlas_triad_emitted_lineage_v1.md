# SPEC — ATLAS TRIAD EMITTED LINEAGE V1

## What this slice is

This slice upgrades the TRIAD family proof so Atlas validates emitted append-only records directly, not only the reference vectors used to create them.

## Purpose

Atlas must prove that the actual emitted records across the TRIAD-facing boundary classify correctly through the common family validator and can be tied together in a deterministic lineage receipt.

## Emitted family records validated

- reference intake records from `data/triad_references.ndjson`
- restore-prep records from `data/triad_restore_prep.ndjson`
- update-prep records from `data/triad_update_prep.ndjson`
- restore-request records from `data/triad_restore_requests.ndjson`
- artifact-apply-request records from `data/triad_artifact_apply_requests.ndjson`

## Product surface

- `scripts/_lib_atlas_triad_emitted_lineage_v1.ps1`
- `scripts/_RUN_atlas_triad_emitted_lineage_full_v1.ps1`
- `docs/SPEC_atlas_triad_emitted_lineage_v1.md`
- `docs/WBS_ATLAS_TRIAD_EMITTED_LINEAGE_v1.md`

## Output surfaces

- `proofs/receipts/atlas_triad_emitted_lineage.ndjson`

## Stable token

- `ATLAS_TRIAD_EMITTED_LINEAGE_FULL_OK`

## Current proof level

Current proof shows:

- the family full runner succeeds
- the latest emitted TRIAD-facing Atlas records are read from append-only ndjson surfaces
- emitted records classify correctly as:
  - `reference`
  - `restore-prep`
  - `update-prep`
  - `restore-request`
  - `artifact-apply-request`
- deterministic lineage receipts are appended

## Boundary rule

Atlas validates and records TRIAD-facing lineage.
TRIAD remains the canonical restore substrate.

## Non-claims

This slice does NOT claim:

- TRIAD restore execution
- Atlas-driven replay
- distributed schedulers
- broad service orchestration
- final Atlas Artifacts completion

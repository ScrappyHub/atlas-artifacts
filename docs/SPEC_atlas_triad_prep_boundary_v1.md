# SPEC — ATLAS TRIAD PREP BOUNDARY V1

## What this slice is

This slice adds deterministic prep surfaces on top of the already-green TRIAD reference intake boundary.

Atlas remains:
- updater
- artifact/orchestration surface
- append-only history and job ledger surface
- TRIAD reference intake surface
- restore-prep / update-prep preparation surface

TRIAD remains:
- restore substrate
- restore execution authority
- replay/restore verification authority

Atlas does not perform restore execution in this slice.

## Slice purpose

Atlas turns accepted TRIAD references into deterministic prep records so downstream governed flows can distinguish:
- reference intake
- restore preparation
- update preparation

without collapsing those responsibilities into one generic job.

## Canonical schemas

- `atlas.triad.reference.v1`
- `atlas.triad.restore_prep.v1`
- `atlas.triad.update_prep.v1`

## Product surface

- `schemas/atlas.triad.restore_prep.v1.json`
- `schemas/atlas.triad.update_prep.v1.json`
- `scripts/_lib_atlas_triad_prep_boundary_v1.ps1`
- `scripts/_RUN_atlas_triad_restore_prep_v1.ps1`
- `scripts/_RUN_atlas_triad_update_prep_v1.ps1`
- `scripts/_RUN_atlas_triad_prep_boundary_smoke_v1.ps1`
- `data/triad_restore_prep.ndjson`
- `data/triad_update_prep.ndjson`

## Stable tokens

- `ATLAS_TRIAD_RESTORE_PREP_OK`
- `ATLAS_TRIAD_UPDATE_PREP_OK`
- `ATLAS_TRIAD_PREP_BOUNDARY_SMOKE_OK`

## Boundary rule

Atlas prepares. TRIAD restores.

## Non-claims

This slice does NOT claim:
- TRIAD restore execution
- Atlas-driven replay
- scheduler/distributed workers
- broad service orchestration
- full Atlas Artifacts completion

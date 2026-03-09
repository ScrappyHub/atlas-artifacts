# SPEC — ATLAS TRIAD FAMILY VALIDATION V1

## What this slice is

This slice consolidates all TRIAD-facing Atlas boundary objects into one common family model with shared validation helpers and a single family full runner.

## Family members

Locked family members:

- `atlas.triad.reference.v1`
- `atlas.triad.restore_prep.v1`
- `atlas.triad.update_prep.v1`
- `atlas.triad.restore_request.v1`
- `atlas.triad.artifact_apply_request.v1`

## Shared family invariants

Every TRIAD-facing Atlas family object must carry:

- `schema`
- `triad_ref`
- `snapshot_ref`
- `source_packet_id`
- `source_content_ref`
- `device_id`
- `captured_utc`

## Family purpose

Atlas must validate the full TRIAD-facing boundary as one governed object family rather than several unrelated adjacent slices.

This slice does not change the role boundary:

- Atlas records, prepares, requests, and orchestrates
- TRIAD restores and replays

## Product surface

- `scripts/_lib_atlas_triad_family_v1.ps1`
- `scripts/_RUN_atlas_triad_family_full_v1.ps1`
- `docs/SPEC_atlas_triad_family_validation_v1.md`
- `docs/WBS_ATLAS_TRIAD_FAMILY_VALIDATION_v1.md`

## Stable token

- `ATLAS_TRIAD_FAMILY_FULL_OK`

## Current proof level

Current proof shows:

- all TRIAD-facing boundary slices run together successfully
- append-only surfaces exist together
- common family validation library is installed
- family full runner succeeds deterministically

Current proof does not yet classify emitted prep/request records directly; that is the next follow-on slice.

## Non-claims

This slice does NOT claim:

- TRIAD restore execution
- Atlas-driven replay
- distributed schedulers
- broad service orchestration
- final Atlas Artifacts completion

## Boundary rule

Atlas governs the TRIAD-facing object family.
TRIAD remains the canonical restore substrate.

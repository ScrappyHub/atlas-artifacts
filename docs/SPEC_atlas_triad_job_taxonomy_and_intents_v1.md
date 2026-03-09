# SPEC — ATLAS TRIAD JOB TAXONOMY AND INTENTS V1

## What this slice is

This slice formalizes deterministic TRIAD-backed job taxonomy and prep intent records on top of the already-green intake + restore-prep + update-prep surfaces.

Atlas remains:
- updater
- artifact/orchestration surface
- append-only history and job ledger surface
- TRIAD reference intake surface
- TRIAD prep surface
- deterministic request/intent recording surface

TRIAD remains:
- restore substrate
- restore execution authority
- replay/restore verification authority

Atlas still does not execute TRIAD restore work in this slice.

## Canonical job taxonomy

Locked job types:
- `triad-reference-intake`
- `triad-restore-prep`
- `triad-update-prep`
- `triad-restore-request`
- `triad-artifact-apply-request`

## Canonical intent schemas

- `atlas.triad.restore_request.v1`
- `atlas.triad.artifact_apply_request.v1`

## Product surface

- `schemas/atlas.triad.restore_request.v1.json`
- `schemas/atlas.triad.artifact_apply_request.v1.json`
- `scripts/_lib_atlas_triad_intents_v1.ps1`
- `scripts/_RUN_atlas_triad_restore_request_v1.ps1`
- `scripts/_RUN_atlas_triad_artifact_apply_request_v1.ps1`
- `scripts/_RUN_atlas_triad_job_taxonomy_and_intents_smoke_v1.ps1`
- `data/triad_restore_requests.ndjson`
- `data/triad_artifact_apply_requests.ndjson`
- `data/jobs.ndjson`

## Purpose

Atlas must be able to express downstream TRIAD-backed work as governed append-only intent records instead of implicit operator actions.

This slice records deterministic requests and job evidence only.

## Stable token

- `ATLAS_TRIAD_JOB_TAXONOMY_AND_INTENTS_SMOKE_OK`

## Boundary rule

Atlas records and prepares.
TRIAD restores and replays.

## Non-claims

This slice does NOT claim:
- TRIAD restore execution
- Atlas-driven replay
- scheduler/distributed workers
- broad service orchestration
- full Atlas Artifacts completion

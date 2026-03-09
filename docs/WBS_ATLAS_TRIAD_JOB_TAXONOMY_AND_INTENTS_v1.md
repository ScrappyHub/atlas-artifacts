# WBS — ATLAS TRIAD JOB TAXONOMY AND INTENTS V1

## 01. Job taxonomy doc
Status: [GREEN]
Notes:
- TRIAD-backed job types made explicit
- taxonomy is now deterministic and named

## 02. Restore request schema
Status: [GREEN]
Notes:
- `atlas.triad.restore_request.v1` defined
- required fields defined

## 03. Artifact apply request schema
Status: [GREEN]
Notes:
- `atlas.triad.artifact_apply_request.v1` defined
- required fields defined

## 04. Intent library
Status: [GREEN]
Notes:
- append-only request writers
- deterministic path helpers

## 05. Restore request runner
Status: [GREEN]
Notes:
- writes restore request
- writes job ledger line
- emits:
  - `ATLAS_TRIAD_RESTORE_REQUEST_OK`

## 06. Artifact apply request runner
Status: [GREEN]
Notes:
- writes artifact apply request
- writes job ledger line
- emits:
  - `ATLAS_TRIAD_ARTIFACT_APPLY_REQUEST_OK`

## 07. Smoke runner
Status: [GREEN]
Notes:
- exercises both request surfaces
- verifies append-only outputs
- emits:
  - `ATLAS_TRIAD_JOB_TAXONOMY_AND_INTENTS_SMOKE_OK`

## Current result
Status: [GREEN]
Notes:
- Atlas now has deterministic TRIAD-backed request intent recording
- Atlas still does not execute TRIAD restore work
- next likely slice is request/intent convergence + common family validation

## Recommended next slice
Status: [YELLOW]
Notes:
- add common snapshot-reference family
- unify prep/request object family validation
- add a single family smoke/full runner
- classify intent kinds deterministically across the TRIAD-facing Atlas boundary

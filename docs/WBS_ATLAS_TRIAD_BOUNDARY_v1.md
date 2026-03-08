# WBS — ATLAS TRIAD BOUNDARY V1

## 01. Boundary contract doc
Status: [GREEN]
Notes:
- Atlas/Triad separation explicit
- Atlas = updater/orchestrator
- TRIAD = restore substrate

## 02. TRIAD reference schema
Status: [GREEN]
Notes:
- schema id locked:
  - `atlas.triad.reference.v1`
- required fields defined
- handoff enum defined

## 03. TRIAD boundary library
Status: [GREEN]
Notes:
- deterministic path helpers
- validation helpers
- append-only write helpers

## 04. TRIAD reference intake runner
Status: [GREEN]
Notes:
- validates reference json
- appends intake record
- appends job-ledger line
- emits stable intake output

## 05. TRIAD boundary smoke
Status: [GREEN]
Notes:
- writes minimal deterministic vector
- runs intake
- verifies append-only surfaces
- stable token:
  - `ATLAS_TRIAD_BOUNDARY_SMOKE_OK`

## Current result
Status: [GREEN]
Notes:
- Atlas can deterministically intake TRIAD references
- Atlas still does not own restore execution
- boundary is explicit, governed, and append-only

## Recommended next slice
Status: [YELLOW]
Notes:
- add TRIAD restore-prep runner
- add TRIAD update-prep runner
- define snapshot reference family
- define job taxonomy for TRIAD-backed work

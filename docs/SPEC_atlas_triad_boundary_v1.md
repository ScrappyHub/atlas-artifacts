# SPEC — ATLAS TRIAD BOUNDARY V1

## What this slice is

This slice defines the deterministic intake boundary between Atlas and TRIAD.

Atlas remains:
- updater
- artifact/orchestration surface
- history / job ledger surface
- packet/reference intake surface

TRIAD remains:
- restore substrate
- restore execution authority
- restore plan / replay surface

Atlas does not become restore execution in this slice.

## Boundary purpose

Atlas must be able to accept and record a deterministic TRIAD reference object so future update / restore-prep flows can operate from a governed reference surface instead of ad hoc paths or operator memory.

## Canonical schema

- `atlas.triad.reference.v1`

## Required reference fields

- `schema`
- `triad_ref`
- `snapshot_ref`
- `source_packet_id`
- `source_content_ref`
- `device_id`
- `captured_utc`
- `handoff_kind`

## Allowed handoff kinds

- `reference-intake`
- `restore-prep`
- `update-prep`

## Current product surface for this slice

- `schemas/atlas.triad.reference.v1.json`
- `scripts/_lib_atlas_triad_boundary_v1.ps1`
- `scripts/_RUN_atlas_triad_reference_intake_v1.ps1`
- `scripts/_RUN_atlas_triad_boundary_smoke_v1.ps1`
- `data/triad_references.ndjson`
- `data/jobs.ndjson`
- `test_vectors/atlas_triad_boundary/minimal_reference.v1.json`

## Atlas responsibilities in this slice

Atlas intake:
- reads TRIAD reference JSON
- validates required fields
- validates stable enum rules
- appends deterministic TRIAD intake record
- appends deterministic job-ledger line
- emits stable success token

## Success token

- `ATLAS_TRIAD_BOUNDARY_SMOKE_OK`

## Non-claims

This slice does NOT claim:
- full TRIAD restore execution
- full Atlas Artifacts completion
- scheduler/distributed workers
- broad service orchestration
- Legacy Doctor wider service boundary

## Role boundary

Atlas is now green on:
- artifact/update Tier-0 surface
- append-only inventory history
- append-only job ledger
- TRIAD reference intake boundary

TRIAD remains the canonical restore substrate.

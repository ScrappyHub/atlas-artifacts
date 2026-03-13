# SPEC — ATLAS TRIAD ONEBUTTON GREEN V1

## What this slice is

This slice creates one authoritative one-button TRIAD-facing Atlas runner that executes the full TRIAD boundary proof chain and writes a deterministic evidence bundle.

## Chain executed

- TRIAD reference intake smoke
- TRIAD prep boundary smoke
- TRIAD job taxonomy and intents smoke
- TRIAD family full runner
- TRIAD emitted lineage full runner

## Evidence bundle

The one-button runner writes a deterministic evidence folder containing:

- transcript
- summary
- sha256sums
- copied lineage receipt snapshot

## Product surface

- `scripts/_RUN_atlas_triad_onebutton_green_v1.ps1`
- `docs/SPEC_atlas_triad_onebutton_green_v1.md`
- `docs/WBS_ATLAS_TRIAD_ONEBUTTON_GREEN_v1.md`

## Output surfaces

- `proofs/receipts/atlas_triad_onebutton_green/<timestamp>/...`

## Stable token

- `ATLAS_TRIAD_ONEBUTTON_GREEN_OK`

## Current proof level

Current proof shows:

- one command executes the full TRIAD-facing Atlas boundary chain
- the one-button runner fails fast deterministically
- a deterministic evidence bundle is written
- lineage receipt snapshot is copied into the evidence bundle

## Boundary rule

Atlas proves the TRIAD-facing boundary in one deterministic run.
TRIAD remains the canonical restore substrate.

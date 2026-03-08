# WBS — Atlas Tier-0 Progress Ledger

## Project definition
Atlas Tier-0 is a deterministic artifact instrument for canonical inventory blobs, verifiable packet emission, packet verification, append-only receipts, and frozen green evidence.

## Current state
Atlas Tier-0 green surface is real and proven locally:
- blob emit: GREEN
- blob verify: GREEN
- packet emit: GREEN
- packet verify: GREEN
- full selftest: GREEN
- negative vectors: GREEN
- full-green runner: GREEN

## WBS status
- 00 Repo/runtime discipline — GREEN
- 01 Shared library + CLI surface — GREEN
- 02 Blob content-ref law — GREEN
- 03 Emit inventory blob flow — GREEN
- 04 Verify blob flow — GREEN
- 05 Emit inventory packet runner — GREEN
- 06 Tier-0 packet verify runner — GREEN
- 07 Full selftest runner — GREEN
- 08 Negative vectors — GREEN
- 09 Full-green runner + frozen latest-green packet — GREEN
- 10 Inventory history ledger — WHITE
- 11 Job ledger surface — WHITE
- 12 TRIAD-facing integration boundary — WHITE
- 13 Packaging / release hygiene — YELLOW

## Locked current green surface
- `src\shared\Atlas.Handoff\`
- `src\tools\Atlas.HandoffCli\`
- `scripts\_RUN_atlas_emit_inventory_smoke_v1.ps1`
- `scripts\_RUN_atlas_tier0_smoke_emit_and_verify_blob_v1.ps1`
- `scripts\_RUN_atlas_emit_inventory_packet_v1.ps1`
- `scripts\_RUN_atlas_tier0_packet_verify_v1.ps1`
- `scripts\_RUN_atlas_tier0_full_selftest_v1.ps1`
- `scripts\_RUN_atlas_tier0_negative_vectors_v2.ps1`
- `scripts\_RUN_atlas_full_green_v1.ps1`
- `test_vectors\atlas_tier0\frozen_latest_green\`
- `proofs\receipts\atlas.ndjson`

## Remaining work to Tier-0 freeze
- write and lock spec
- write and lock WBS/progress ledger
- commit intentional product surface only
- remove or ignore scratch / obsolete patch clutter
- tag frozen green state

## Definition of Done
Atlas Tier-0 is complete when the green surface is frozen in-repo, documented, intentionally committed, and reproducible on a clean machine using only the canonical runner set.

## Locked next order
1. Freeze exact green surface in-repo
2. Commit intentional product surface plus frozen vectors/evidence references
3. Build deterministic inventory history ledger
4. Build deterministic job ledger
5. Lock TRIAD-facing integration contract

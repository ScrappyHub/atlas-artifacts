# Atlas Agent — Canonical Rollback Engine (Contract)

This document is the non-negotiable, canonical contract for rollback behavior in Atlas.Agent.

## Purpose

Rollback provides a deterministic safety mechanism:

1) Snapshot (zip + manifest)
2) Apply changes (update/install)
3) If failure => restore snapshot

This is designed to be offline-first and auditable.

## Snapshot layout

Snapshots are stored under runs:

C:\ProgramData\Atlas\Agent\runs\<runId>\snapshot\
  snapshot.zip
  manifest.json

The snapshot MUST include the contents of the source directory being protected.

## Manifest requirements

`manifest.json` MUST include:
- `run_id`
- `created_at_utc`
- `source_dir`
- `snapshot_zip`
- file list entries with:
  - relative path
  - size
  - sha256

The manifest is used for auditability and to verify snapshot integrity.

## Restore rules

Restore MUST:
- Extract snapshot to a temp folder
- Validate no zip path traversal
- Replace the target directory contents atomically-ish:
  - delete target directory (best-effort)
  - move/extract into place
- Fail deterministically with actionable error messages

## Future upgrades

VSS / Volume Shadow Copy is a future enhancement.
The v1 engine is zip + manifest and is valid for production baseline.

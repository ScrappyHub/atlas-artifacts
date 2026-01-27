<<<<<<< HEAD
# Runs & Artifacts

Atlas Update treats every scan/apply as a “run” with artifacts.

## Run Types
- scan_run: inventory + resolve only
- apply_run: inventory + resolve + plan + apply + verify

## Run Stages
1. inventory
2. resolve
3. plan
4. apply
5. verify
6. record

## Canonical Artifacts (files)
- inventory.json
- candidates.json
- plan.json
- apply.log (or apply.jsonl)
- verify.json
- summary.json (human readable outcomes)
- manifest.json (hashes of all artifacts, chain-ready)

## Required Fields
- run_id (UUID)
- created_at
- initiated_by (user/service)
- policy_snapshot_id
- engine_set (engine_ids used)
- outcome (success|partial|failed|deferred)
- failure_reasons (structured)
=======
# Runs & Artifacts

Run types:
- scan_run
- apply_run

Stages:
inventory → resolve → plan → apply → verify → record

Artifacts (required):
inventory.json, candidates.json, plan.json, apply.jsonl, verify.json, summary.json, manifest.json
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

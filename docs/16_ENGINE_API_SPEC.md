# Engine API Spec (Agent ↔ Engines)

Engines are in-process modules (built-in) in Phase 1.

## Required Functions
- inventory(ctx) -> InventoryResult
- check_updates(ctx, inventory) -> CandidateResult
- build_plan(ctx, candidates, policy) -> Plan
- apply(ctx, plan) -> ApplyResult
- verify(ctx, applied) -> VerifyResult

## Determinism Rule
- build_plan must be deterministic given:
  - inventory snapshot
  - candidates snapshot
  - policy snapshot
  - engine version
- Any non-deterministic data (timestamps, random IDs) must be excluded from plan hash inputs.

## Provenance Fields (required)
Every candidate must include:
- engine_id
- source_type (package_manager|vendor_feed|store)
- source_id (e.g. winget source name)
- package_id (ecosystem identifier)
- current_version
- candidate_version
- requires_admin (bool)
- evidence: signature/hash fields when available

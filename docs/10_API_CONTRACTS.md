# API Contracts (Internal)

## UI → Agent (IPC)
Commands:
- request_scan
- request_apply_plan
- request_apply_execute
- get_run_history
- get_current_inventory
- set_policy
- set_schedule
- approve_update (when required)

All IPC requests must include:
- user_id
- requested_action
- optional approval token (for privileged actions)

## Agent → Engine (in-process interface)
Functions (canonical):
- inventory(ctx) -> InventoryResult
- check_updates(ctx, inventory) -> CandidateUpdates
- build_plan(ctx, candidates, policy) -> Plan
- apply(ctx, plan) -> ApplyResult
- verify(ctx, applied) -> VerifyResult

Every result must include provenance fields:
- engine_id
- source descriptors
- evidence (signature/hash if available)

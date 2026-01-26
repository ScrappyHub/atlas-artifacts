# IPC Spec (UI ↔ Agent)

## Transport
- Windows named pipe (Phase 1) OR localhost HTTPS with mTLS (Phase 2+).
- Phase 1 recommendation: Named pipe with per-user ACLs.

## AuthN/AuthZ
- UI sends user_id + session token (agent issues token after OS user validation).
- Agent authorizes every request using:
  - user role
  - feature flags
  - global policy
  - per-app policy
  - required privileges

## Commands (canonical)
- get_status
- get_inventory
- request_scan
- get_candidates (latest scan results)
- build_plan
- execute_plan
- get_run_history
- get_run_details
- set_global_policy
- set_app_policy
- set_feature_flag

<<<<<<< HEAD
# Policy Model

## Roles
- Admin: configure engines/sources, global policy, schedules, approvals
- Maintainer: approve updates within policy
- User: manage personal preferences within constraints, request scans
- Auditor: read-only access to history

## Feature Flags (per device + user)
- enable_background_scans (default true)
- enable_auto_updates (default false)
- allow_community_sources (default false)
- allow_prerelease_versions (default false)
- allow_driver_updates (default false)
- allow_silent_install (default false)

## Per-App Policy
- mode: AUTO | NOTIFY | NEVER
- ring: stable | beta (default stable)
- source_lock: optional engine_id
- constraints:
  - only_ac_power
  - min_battery_percent
  - only_unmetered
  - allowed_time_windows
  - requires_approval (default true)

## Global Policy
- global_auto_updates: OFF by default
- require_approval_for_installs: ON by default
- blocklist/allowlist categories (future)

## Evaluation Rules (canonical)
1. If global_auto_updates = OFF → no automatic apply runs.
2. If app.mode = NEVER → block apply for that app.
3. If engine capability required and feature flag denies → block candidate.
4. If candidate requires admin and caller role < Admin → NOTIFY only.
5. If constraints not satisfied (battery, window, metered) → defer and record reason.
=======
# Policy Model

Roles:
- admin, maintainer, user, auditor

Feature flags (per device + user):
- enable_background_scans
- enable_scheduling
- enable_auto_updates
- allow_community_sources
- allow_silent_install
- enable_org_controls

Per-app policy:
- mode: AUTO/NOTIFY/NEVER
- ring: stable/beta
- source_lock_engine_id optional
- constraints: AC, battery, metered, time windows, requires_approval

Evaluation rules:
1) global_auto_updates OFF → no auto apply
2) app NEVER → blocked
3) capability denied by flags → blocked
4) requires admin + role insufficient → notify-only
5) constraints not met → defer w reason
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

<<<<<<< HEAD
# UI Screens Map (Figma → React/Vue)

## 1) Dashboard
- Status: Licensed / Trial / Expired
- Last scan time
- Update summary: X updates available, Y blocked (policy)
- Primary actions: Scan Now, Review Updates

## 2) Updates
- Table: App | Current | Latest | Source | Requires Admin | Mode | Action
- Filters: available / blocked / requires approval / requires admin
- Per-item: approve + apply (if allowed)

## 3) Policies
- Global toggles:
  - Auto updates (OFF default)
  - Require approval for installs (ON default)
  - Allow community sources (OFF default)
  - Allow silent install (OFF default)
- Per-app policy editor:
  - AUTO / NOTIFY / NEVER
  - constraints (AC power, battery, window)

## 4) Schedule
- Enable schedule (tier-gated)
- Frequency: daily/weekly/monthly
- Time window: allowed hours
- Constraints: AC power, min battery, metered network

## 5) Runs (History)
- List runs with outcome badges
- Click run → artifacts summary:
  - what changed
  - failures and reasons
  - verification results

## 6) License & Devices
- Tier + device limit
- This device status: enrolled/active
- Device list (Phase 2+):
  - device_id (short) + nickname + last_seen
  - deactivate device (admin)

## 7) Settings
- Engine visibility (read-only in MVP, admin in future)
- Export run bundle
- Log level
=======
# UI Screens Map

Dashboard, Updates, Policies, Schedule, Runs, License & Devices, Settings.
UI is deferred until backend is fully locked.
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

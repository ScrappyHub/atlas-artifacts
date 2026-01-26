# Requirements

## Functional Requirements (FR)
FR-01 Inventory installed applications and versions.
FR-02 Resolve update candidates via allowed engines.
FR-03 Present updates in UI with provenance and required privileges.
FR-04 Apply updates with an explicit plan and ordering.
FR-05 Enforce per-app policy modes: AUTO, NOTIFY, NEVER.
FR-06 Enforce global policy: auto updates OFF (hard disable).
FR-07 Scheduling: daily/weekly/monthly + time windows.
FR-08 Constraints: AC power, min battery %, metered network, active hours.
FR-09 Roles: Admin, Maintainer, User, Auditor (read-only).
FR-10 Feature flags: enable/disable engines and capabilities per user/device.
FR-11 Maintain a run history with artifacts and searchable outcomes.
FR-12 Provide rollback where supported; otherwise use OS restore points when available.

## Non-Functional Requirements (NFR)
NFR-01 Security: no untrusted installer execution.
NFR-02 Integrity: record what executed, where it came from, and what changed.
NFR-03 Reliability: interruptions resume safely; partial failures are contained.
NFR-04 Transparency: explain why an update requires elevation or is blocked.
NFR-05 Performance: inventory in < 10 seconds for typical machines (MVP).
NFR-06 Offline behavior: UI shows last known inventory and last scan timestamp.
NFR-07 Privacy: default local-only, no telemetry unless explicitly enabled.

## Compliance / Policy Defaults
- Community sources are OFF by default (configurable).
- Driver/firmware updates OFF by default.
- Pre-release versions OFF by default.

# Device Enrollment (Canonical)

Device enrollment is the mechanism that binds a machine to an account and consumes a device slot.

## Device Identity
Device identity must be:
- stable across reboots
- unique enough to prevent collisions
- privacy-preserving (no raw serials in logs)

Canonical approach:
- device_id = SHA-256( normalized_machine_fingerprint + per-install salt )

Where fingerprint inputs may include (OS-specific):
- Windows: MachineGuid + system drive volume serial (careful) + CPU info (optional)
- macOS: hardware UUID
- Linux: machine-id

Never store raw identifiers in artifacts; store only the derived device_id.

## Enrollment States
- pending (created but not confirmed)
- active (consumes a slot)
- deactivated (slot freed)
- revoked (server says no)
- expired (license expired / grace ended)

## Enrollment Actions
- enroll_device: bind this device to account, become active
- deactivate_device: free a slot (Phase 2+ typically affects another device)
- refresh_license: re-validate subscription and entitlements

## Canonical Rule
Only the Agent can:
- compute device_id
- activate enrollment
- apply entitlements

UI can request actions but cannot force outcomes.

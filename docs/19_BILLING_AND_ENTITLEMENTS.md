<<<<<<< HEAD
# Billing & Entitlements (Canonical)

Atlas Update uses a subscription tier to determine:
- max enrolled devices per account
- feature entitlements (feature flags) granted to enrolled devices
- enterprise capabilities (server mode later)

## Tiers (Locked)
Tier 1: Personal — 1 device
Tier 2: Pro/Family — 10 devices
Tier 3: Enterprise — policy-based (unlimited or negotiated)

## Canonical Definitions
- Account: the billing owner and root of entitlements.
- Subscription: the active plan attached to an account.
- Device: a unique machine identity.
- Enrollment: a device authorized under an account, consuming one slot.
- Entitlements: feature permissions granted by tier and overrides.

## Enforcement Strategy (Phase 1 vs Phase 2)
Phase 1 (local-only):
- License token (file-based) determines account_id, tier, expiry, and max_devices.
- Enrollment is stored locally per device.
- UI prevents “extra device” semantics by design (since it’s single device local-only),
  but the data model is future-compatible.

Phase 2 (server mode):
- Server becomes source of truth for:
  - active subscription
  - enrolled device list and slot limits
  - revocations and renewals
- Agent must phone home (or periodically sync) to validate.

## “Over Limit” Behavior (Canonical)
- If max device slots are reached:
  - New enrollments are blocked.
  - UI must show enrolled devices and allow deactivation of one device to free a slot.
- Existing enrolled devices continue operating (soft enforcement) unless:
  - subscription is expired beyond grace period (policy), or
  - enterprise policy specifies hard enforcement (enterprise only).

## Grace Period (Default)
- Default grace period: 7 days after expiration (Phase 2+).
- Phase 1 can implement “expiry disables premium entitlements” without remote calls.

## Entitlement Philosophy
- Tier decides the ceiling (max devices + advanced automation).
- Feature flags control execution gates.
- Agent is the enforcement point; UI is never trusted for enforcement.
=======
# Billing & Entitlements (Canonical)

Tiers (LOCKED):
- Tier 1 Personal: 1 device
- Tier 2 Pro/Family: 10 devices
- Tier 3 Enterprise: policy-based

Definitions:
account, subscription, device, enrollment, entitlements

Enforcement:
Agent is enforcement point. Phase 1 local token; Phase 2 server.
Over-limit blocks new enrollments; existing devices continue (soft enforcement default).
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

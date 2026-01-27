<<<<<<< HEAD
# ADR 0002: Tiering and Device Limits

## Status
Accepted

## Context
Atlas Update needs simple, canonical subscription tiers and device limits.

## Decision
We lock a 3-tier model:
- Tier 1: 1 device
- Tier 2: 10 devices
- Tier 3: Enterprise (policy-based limits)

## Consequences
- Clear marketing and UX
- Straightforward entitlement enforcement
- Future server mode can enforce device slots centrally

## Alternatives Considered
- Two “Tier 2” options (3 devices and 10 devices)
Rejected due to buyer confusion and entitlement/billing complexity.
=======
# ADR 0002: Tiering and Device Limits

## Status
Accepted

## Decision
3 tiers locked:
Tier 1: 1 device
Tier 2: 10 devices
Tier 3: Enterprise (policy-based)
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

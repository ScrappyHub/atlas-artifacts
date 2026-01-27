<<<<<<< HEAD
# Atlas Update

Atlas Update is a workstation software update orchestrator designed for safe automation, explicit user control, and auditable execution.

## Non-Goals / Boundaries
Atlas Update is a standalone product and codebase. It is **not** CORE, not Covenant Gate, and not ROOTED. No shared assumptions, no shared registries, no shared schemas. Only architectural patterns may be reused (registry + capability gating + feature flags + auditable runs).

## Core Features
- Inventory installed software
- Resolve available updates from trusted sources (engines)
- Apply updates under user policy (AUTO / NOTIFY / NEVER)
- Schedule updates (with constraints)
- Role/capability gating
- Auditable runs with artifacts (inventory → plan → apply → verify)

## Safety Defaults (hard rules)
- No arbitrary web scraping for installers.
- Only trusted engines with verifiable provenance:
  - OS package managers
  - vendor-signed feeds
  - stores/APIs with signature verification
- Every install step must have a verifiable origin + recorded artifacts.

## MVP Target
Phase 1 targets Windows using the `winget` engine, with manual approval and full run logging.

## Repository Manifest
See `docs/00_MANIFEST.md`.
=======
# Atlas Update

Atlas Update is a workstation software update orchestrator designed for safe automation, explicit user control, and auditable execution.

## System Boundary (Hard Rule)
Atlas Update is a standalone product and codebase. It is **not** CORE, not Covenant Gate, and not ROOTED. No shared schemas, registries, assumptions, or deployments. Only architectural patterns may be reused (registry + capability gating + feature flags + auditable runs).

## MVP Target
Phase 1: Windows winget scan + auditable run artifacts.

See docs/00_MANIFEST.md.
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

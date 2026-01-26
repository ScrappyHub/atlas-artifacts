# Scope

## In Scope
- Local agent/service for inventory, update resolution, applying updates
- Desktop UI for policies, scheduling, approvals, and viewing run history
- Engine registry and built-in engines (starting with Windows winget)
- Local persistence (SQLite) for policies and run artifacts metadata
- Verifiable provenance: signature/hash verification where applicable

## Out of Scope (initially)
- Enterprise multi-device server control plane (future phase)
- Driver/firmware updates (default-off, future)
- Arbitrary “download from URL” installs
- Browser extension updates outside official channels
- “Optimizer” or “cleanup” tooling

## Hard Boundary Statement
Atlas Update is a standalone system and must never be merged conceptually or operationally with any other platform. Shared patterns are allowed; shared data, schemas, or registries are not.

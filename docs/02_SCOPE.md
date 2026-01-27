<<<<<<< HEAD
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
=======
# Scope

## In Scope
- Local agent/service for inventory, resolution, and execution
- Desktop UI later (backend-first now)
- Engine registry + built-in engines (starting with Windows winget)
- Local persistence (SQLite) + file artifacts

## Out of Scope (initially)
- Arbitrary URL downloads / web scraping installers
- Driver/firmware updates (default-off, later)
- Enterprise server control plane (later)

## Hard Boundary
Standalone system; no coupling with any other platforms.
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

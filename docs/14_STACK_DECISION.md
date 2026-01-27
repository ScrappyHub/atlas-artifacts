<<<<<<< HEAD
# Stack Decision (Phase 1)

## Agent
- .NET (C#) Windows Service
- Rationale: best support for service lifecycle, secure IPC, privilege boundaries, and robust process execution.

## UI
- Tauri shell + web frontend (React/Vue acceptable)
- Rationale: small attack surface, good desktop UX, clean separation from privileged agent.

## Engines
- Built-in only for MVP
- Initial engine: windows.winget

## Non-Goals
- Third-party engine plugins (deferred)
- Remote control plane (deferred)
=======
# Stack Decision (Phase 1)

Agent: .NET (C#) Windows service style
IPC: Named pipe (length-prefixed duplex)
UI: deferred (backend-first); later Tauri + React from Figma
Engines: built-in only (initial windows.winget)
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

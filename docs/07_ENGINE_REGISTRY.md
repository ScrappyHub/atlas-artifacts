<<<<<<< HEAD
# Engine Registry

Engines are the only way Atlas Update interacts with the outside world for updates.
Engines declare their capabilities and trust properties.

## Engine Capabilities
- inventory
- check_updates
- download
- install
- rollback
- verify

## Engine Record
- engine_id (string, unique)
- name
- supported_os: [windows|macos|linux]
- trust_level: system | vendor | community
- requires_admin: boolean
- capabilities: array
- allowed_by_default: boolean
- notes / limitations

## Engine Contract (high-level)
- Engine must not execute arbitrary shell commands outside the contract.
- Engine must return structured results with provenance fields.
- Engine must support deterministic plan generation: same inputs → same plan.
- Engine must emit verification evidence after install when possible.

## Built-in Engines (initial)
- windows.winget (trust_level=system/vendor depending on source, MVP)
=======
# Engine Registry

Engines are the only allowed interface to outside update ecosystems.

Capabilities:
inventory, check_updates, download, install, rollback, verify

Engine record:
engine_id, supported_os, trust_level, requires_admin, capabilities, allowed_by_default
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

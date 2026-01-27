# Atlas Agent — Canonical Licensing Contract

This document is the non-negotiable, canonical contract for licensing in Atlas.Agent.

## Goals (non-negotiable)

- The agent MUST run offline.
- Licensing MUST be enforced locally at runtime.
- Editing license JSON MUST NOT unlock features (signature required).
- License tier MUST cap allowed features and device_limit.

## Files on disk (Windows)

All agent data lives under ProgramData:

C:\ProgramData\Atlas\Agent\
  licenses\
    atlas.license.json
    atlas.license.sig
  runs\
  logs\
  atlas.db

The agent MUST create these directories if missing.

## License payload file

Filename: `atlas.license.json`

Canonical JSON shape:

```json
{
  "license_id": "lic_01JATLAS8F3K",
  "customer": "Acme Corp",
  "tier": "pro",
  "device_limit": 10,
  "features": [
    "winget_scan",
    "artifacts",
    "rollback",
    "export_logs"
  ],
  "issued_at": "2026-01-26T00:00:00Z",
  "expires_at": null
}

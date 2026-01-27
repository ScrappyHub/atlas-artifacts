# Atlas Agent — Canonical Licensing (Contract)

This document is the non-negotiable, canonical contract for licensing in Atlas.Agent.

## Files on disk (Windows)

All licensing files live under ProgramData:

C:\ProgramData\Atlas\Agent\
  licenses\
    atlas.license.json
    atlas.license.sig

Additional agent data:

C:\ProgramData\Atlas\Agent\
  runs\
  logs\
  atlas.db

The agent MUST be able to run offline. Licensing is enforced locally.

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

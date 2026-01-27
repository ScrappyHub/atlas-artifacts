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

Canonical meaning of fields

    license_id: unique ID (string)

    customer: display name (string)

    tier: one of free | personal | pro | business | enterprise

    device_limit: maximum devices allowed (integer)

    features: list of feature slugs (strings)

    issued_at: ISO-8601 UTC timestamp

    expires_at: ISO-8601 UTC timestamp OR null

Canonical feature slugs

    winget_scan

    artifacts

    rollback

    export_logs

    automation

Signature file

Filename: atlas.license.sig

Format: Base64-encoded RSA-SHA256 signature computed over the EXACT bytes of atlas.license.json.

    Algorithm: RSA + SHA-256

    Padding: PKCS#1 v1.5

    Verification: performed locally by Atlas.Agent using embedded public key

If signature verification fails, the agent MUST treat the machine as unlicensed.
Tier policy (canonical caps)

Tier defines an upper bound. The payload may request LESS than the tier allows, but NEVER MORE.

The agent clamps requested features to the tier’s allowed feature set:

allowed_features = requested_features AND tier_features

The agent clamps requested device_limit to tier max:

allowed_device_limit = min(requested_device_limit, tier_max_devices)
Canonical tiers

    free:

        max devices: 1

        features: winget_scan

    personal:

        max devices: 1

        features: winget_scan, artifacts

    pro:

        max devices: 5

        features: winget_scan, artifacts, export_logs

    business:

        max devices: 25

        features: winget_scan, artifacts, export_logs, rollback

    enterprise:

        max devices: 250

        features: winget_scan, artifacts, export_logs, rollback, automation

Runtime enforcement

All feature access MUST be gated in the command router / handlers.
If a feature is not licensed, the agent MUST return a failure response.

This is enforcement, not UI.
Expiration

If expires_at is not null and is in the past (UTC), the license is expired and MUST be treated as unlicensed.
Offline operation

No network call is required to validate the license.
All validation is local:

    verify signature

    apply tier caps

    enforce at runtime

Atlas Agent — Canonical Licensing Contract

This document defines the non-negotiable licensing rules for Atlas.Agent.

The agent MUST operate fully offline.
All license validation is local.

Disk Layout (Canonical)

Windows
C:\ProgramData\Atlas\Agent\

licenses

runs

logs

atlas.db

macOS
/Library/Application Support/Atlas/Agent/

Linux
/var/lib/atlas/agent/

License File

Filename: atlas.license.json

Example payload:

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

Field Meanings

license_id
Unique license identifier

customer
Display name

tier
free | personal | pro | business | enterprise

device_limit
Maximum number of devices allowed

features
List of feature slugs

issued_at
ISO-8601 UTC timestamp

expires_at
ISO-8601 UTC timestamp or null

Feature Slugs (Canonical)

winget_scan
artifacts
rollback
export_logs
automation

Signature File

Filename: atlas.license.sig

Format: Base64 RSA-SHA256 signature over the EXACT bytes of atlas.license.json

Algorithm: RSA + SHA-256
Padding: PKCS#1 v1.5

If verification fails, the agent MUST treat the machine as UNLICENSED.

Tier Caps (Canonical Enforcement)

The tier defines the maximum allowed capabilities.

Requested values may be LOWER but never HIGHER.

Allowed features = requested features AND tier features
Allowed devices = minimum(requested, tier maximum)

Tier Matrix

free
max devices: 1
features: winget_scan

personal
max devices: 1
features: winget_scan, artifacts

pro
max devices: 5
features: winget_scan, artifacts, export_logs

business
max devices: 25
features: winget_scan, artifacts, export_logs, rollback

enterprise
max devices: 250
features: winget_scan, artifacts, export_logs, rollback, automation

Expiration

If expires_at is set and is in the past (UTC), the license is expired and treated as unlicensed.

Runtime Enforcement

Every feature MUST be enforced in the command router.

If a feature is not licensed, the agent MUST return failure.

This is enforcement, not UI.

Offline Validation Flow

Verify signature

Apply tier caps

Enforce gates in router

No network calls are allowed.

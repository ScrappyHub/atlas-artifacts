Format:

Base64 text of an RSA-SHA256 signature over the exact bytes of atlas.license.json.

Algorithm:

RSA

SHA-256

PKCS#1 v1.5 padding

Verify using the embedded PUBLIC key (agent ships public key only)

Private key is held by the issuer (server-side signer)

Verification requirements

The agent MUST:

Read json bytes exactly from disk

Read signature base64 from .sig

Verify signature against json bytes using RSA-SHA256 (PKCS#1 v1.5)

Parse JSON into strongly typed model

Validate required fields and time validity:

If expires_at is set and now > expires_at => invalid

Produce an in-memory effective License (features as bitflags)

If verification fails:

License is invalid

Agent should still run baseline operations that do not require paid features (policy is enforced by router gates)

Feature strings (canonical)

winget_scan

artifacts

rollback

export_logs

automation

These map to Atlas.Agent.Licensing.Feature flags.

Enforcement (non-negotiable)

UI must NOT be required for enforcement.

All privileged functionality MUST be guarded at runtime:

If license invalid => paid feature commands return a deterministic failure response

If feature not enabled => return deterministic failure: "Feature not licensed."

Example:

Rollback requires Feature.Rollback

Export requires Feature.ExportLogs

Device limits

Device limits are enforced via deterministic device fingerprinting (not hardware attestation).
The license can optionally be enforced using a device registry in the future.
For now, the agent exposes DeviceIdentity.GetDeviceId() which can be used for tracking and later enforcement.

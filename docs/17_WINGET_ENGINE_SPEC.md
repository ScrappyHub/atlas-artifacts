<<<<<<< HEAD
# windows.winget Engine Spec

## Goals (MVP)
- Inventory installed packages (winget list)
- Determine upgradeable packages (winget upgrade)
- Install updates with explicit package IDs (winget upgrade --id ...)
- Capture logs for apply artifacts
- Verify by re-reading inventory and confirming version bump

## Allowed Executions
The agent may execute ONLY these winget commands (no freeform args):
- winget --version
- winget source list
- winget list --accept-source-agreements
- winget upgrade --accept-source-agreements
- winget upgrade --id <PACKAGE_ID> --accept-source-agreements --accept-package-agreements [--silent if allowed]
- winget show --id <PACKAGE_ID> --accept-source-agreements

## Parsing Rules
- Output parsing must be resilient to localization:
  - Prefer JSON output if available (`--output json` where supported)
  - If not supported in target winget version, parse table with strict column detection.

## Safety Defaults
- If package is from an untrusted source and allow_community_sources=OFF → candidate blocked
- If requires_admin and caller role < admin → candidate becomes NOTIFY-only
- If allow_silent_install=OFF → do not pass --silent

## Known Limitations
- Not all packages support silent updates
- Some upgrades may require user interaction; these must be NOTIFY-only or explicitly approved
=======
# windows.winget Engine Spec

Allowlisted commands:
- winget list --accept-source-agreements [--output json]
- winget upgrade --accept-source-agreements [--output json]
- (apply later) winget upgrade --id <ID> --accept-* [--silent gated]
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

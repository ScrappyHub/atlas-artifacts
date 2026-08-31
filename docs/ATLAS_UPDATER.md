# Atlas Updater (Windows) — Operator Guide

Real, signed, policy-gated software + OS update manager built on the Atlas Tier-0 evidence spine.
Every mutating action emits an Ed25519-signed Atlas commitment (see ATLAS_SPEC.md). Windows is the
proven-first platform; macOS and Linux adapters follow the same engine/evidence contract.

## Capabilities & status (proven on ANAKIN)

| Capability | Engine | Status |
|---|---|---|
| Full inventory (registry all hives + Store/MSIX + winget + updater tasks/services) | `atlas_software_inventory_full_v1.ps1` | GREEN (1155 apps, signed+verified) |
| Passive block of an app updater (disable scheduled tasks/services, winget pin) | `atlas_enforce_block_v1.ps1` | GREEN (real, net-zero lifecycle) |
| Automatic re-enforcement on drift | `atlas_reconcile_agent_v1.ps1` | GREEN |
| Persistent SYSTEM enforcement (startup + hourly) | `atlas_install_reconcile_task_v1.ps1` | Built (install = admin) |
| Update (policy-gated, held/blocked refused) | `atlas_update_apply_v1.ps1` | GREEN (dry + block signed) |
| Version rollback (`winget install --version`) | `atlas_rollback_version_v1.ps1` | Built (dry signed) |
| OS update scan (`Microsoft.Update.Session`) | `atlas_windows_update_adapter_v1.ps1` | Real COM scan |
| Enforcement rollback (restore prior task/service state) | `atlas_enforce_block_v1.ps1 -Action unblock` | GREEN |

## Safety model

- **Plan/dry-run by default.** Mutations require `-Mode apply -IUnderstand`.
- **Admin-gated.** Disabling updater tasks/services and OS-update holds require elevation.
- **Reversible.** Every enforcement captures prior state; every update records a version-rollback command.
- **Fail-closed policy.** `runtime/software_fleet/windows_policy.json` (`hold`/`blacklisted`/`protected`)
  refuses updates to listed apps. Core Windows-servicing services are never touched.
- **Signed evidence.** Every action -> `emit-artifact` -> signed Option-A packet in `data/outbox`.

## Operator commands (from repo root)

```
# Inventory (read-only)
RUN_ATLAS_INVENTORY.cmd
# Block plan (safe, no admin)
powershell -File scripts\engine\atlas_enforce_block_v1.ps1 -Target "Opera GX scheduled" -Action block -Mode plan
# Block apply (admin) / undo
powershell -File scripts\engine\atlas_enforce_block_v1.ps1 -Target "<app>" -Action block   -Mode apply -IUnderstand
powershell -File scripts\engine\atlas_enforce_block_v1.ps1 -Target "<app>" -Action unblock -Mode apply -IUnderstand
# Install persistent guard (admin)
RUN_ATLAS_INSTALL_GUARD.cmd
# Update (dry) / apply
powershell -File scripts\engine\atlas_update_apply_v1.ps1 -Id "7zip.7zip" -Mode dry
powershell -File scripts\engine\atlas_update_apply_v1.ps1 -Id "<id>" -Mode apply -IUnderstand
# Version rollback
powershell -File scripts\engine\atlas_rollback_version_v1.ps1 -Id "<id>" -Version "<v>" -Mode apply -IUnderstand
# Prove everything green (admin, net-zero)
RUN_ATLAS_UPDATER_GREEN.cmd
```

## Integration points (ecosystem)

- **TRIAD** is the intended deterministic capture/restore backend for rollback: a rollback commitment
  should reference a TRIAD sealed snapshot rather than only a `winget --version` command.
- **legacy-doctor** and other consumers read the signed inventory / update / enforcement packets;
  they never reach into Atlas internals (boundary law).

## Roadmap

- macOS (`brew`, launchd updaters, `softwareupdate`) and Linux (`apt`/`dnf`/`snap`/`flatpak`, systemd
  timers) adapters implementing the same engine + signed-evidence contract.
- OS-update hold via reversible Windows Update deferral policy.
- Fleet/dashboard + Atlas Cloud (post standalone).

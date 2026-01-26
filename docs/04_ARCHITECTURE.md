# Architecture

## Components
1. Agent (daemon/service)
   - Executes scans, resolves updates, performs installs, writes artifacts.
2. UI Client
   - Displays inventory, updates, policies, schedules, run history.
3. Engine Layer
   - Built-in engines that implement a strict contract.
4. Persistence
   - SQLite DB for config/policy/registry/run metadata.
   - Artifact store (files) for run payloads and logs.

## Trust Boundaries
- UI is untrusted for execution: UI requests actions; Agent authorizes and executes.
- Engines are privileged code: only built-in engines initially (no third-party plugins).
- Installers are the highest risk: must have verifiable provenance and recorded hashes/signatures.

## Data Flow (Run Lifecycle)
Inventory → Resolve → Plan → Apply → Verify → Record
- Each stage emits a signed/hashed artifact bundle (hash-chain ready).

## OS Targets
- Phase 1: Windows (winget)
- Phase 2+: macOS (brew), Linux (apt/dnf), stores later.

## “No Scrape” Rule
Atlas Update never scrapes random websites for installers. Engines may only use trusted package managers or vendor endpoints with verification.

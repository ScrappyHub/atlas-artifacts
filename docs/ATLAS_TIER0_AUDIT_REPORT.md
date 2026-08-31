# Atlas Tier-0 — Audit & Completion Report

Date: 2026-08-31 · Repo: `C:\dev\atlas-update` · Branch: `atlas/tier0-signed-inventory`
Method: every claim below was re-derived from on-disk bytes independently (Python + a fresh
Windows toolchain run), **not** taken from any pre-existing "GREEN" label.

## 1. Verdict

**Before:** the substrate was real but the headline claim was not. The signed-commitment engine
existed yet nothing called it; `emit-inventory` produced only a blob; 58 of 59 outbox packets were
unsigned "pointer packets" that violated the written constitution; and a second verifier had been
introduced that *accepted* those violations (bare-hex packet_id, no signature).

**After:** the Tier-0 theorem is demonstrated end-to-end on real execution, and sealed in a
self-verifiable freeze. Runner emits `ATLAS_TIER0_FULL_GREEN_OK`.

## 2. What the audit found (independently verified)

- Blob store sound: 79/79 blobs satisfied `sha256(bytes)==name`.
- Only the Feb-16 flagship packet (`d61aa81f…`) met the written constitution and carried a real
  Ed25519 signature — but its `content_ref` was the literal string `"sealed"`, binding to nothing.
- 58/59 packets were non-conforming: `packet_id.txt` held **bare hex** (no `sha256:` prefix) and
  `sha256sums.txt` **listed itself** (a self-hash that can never verify). No commitment, no signature.
- Two contradictory verifiers shipped together. `_RUN_atlas_tier0_packet_verify_v1.ps1` *required*
  bare-hex packet_id (opposite of the written rule) and checked no signature — its greens proved a
  weaker, redefined constitution.
- `emit-inventory` (C#) ran only 3 of 9 stages (snapshot→blob), then stopped.
- The inventory "pledge" (`data/pledges.ndjson`) was an unsigned, unchained journal — not the
  hash-linked append-only ledger the engine actually supports (`data/pledge/pledge.ndjson`).
- The June "governance freezes" were a different subsystem, not the Tier-0 artifact theorem.

Root cause: `HandoffEngine` (CommitAndSign → PledgeLocal → Option-A packet) was correct and unused;
the CLI and a PowerShell pipeline went around it and drifted.

## 3. What was changed

- **`src/tools/Atlas.HandoffCli/Program.cs`** — `emit-inventory` rewritten to run the full chain via
  `HandoffEngine`: snapshot → blob → commitment → **Ed25519 signature** → hash-linked pledge →
  Option-A packet, binding the commitment to the **real** `content_ref`. Added `--captured-utc`
  (determinism) and `--nfl-url`. Added `verify-pledge` (recomputes the pledge chain with the
  canonical canonicalizer, so tamper detection is correct by construction).
- **`scripts/_VERIFY_atlas_signed_inventory_packet_v1.ps1`** — one canonical verifier: written
  Option-A constitution **+ Ed25519 signature verify (ssh-keygen -Y verify) + content_ref→blob
  resolution**. Rejects the self-referential sums row and bare-hex packet_id.
- **`scripts/_RUN_atlas_tier0_negative_vectors_signed_v1.ps1`** — 11 attack vectors.
- **`scripts/_RUN_atlas_tier0_signed_green_v1.ps1`** — one-command runner; `ATLAS_TIER0_FULL_GREEN_OK`
  only after all positive+negative proofs, then writes a freeze.
- **`scripts/_RUN_atlas_tier0_freeze_v1.ps1`** — self-verifiable freeze receipt.
- **`docs/ATLAS_SPEC.md`** — normative Tier-0 spec; marks the weaker verifier and the unsigned
  emitter as deprecated.

## 4. Proven (last full run)

| Proof | Result |
|---|---|
| Clean build | PASS (0 warnings, 0 errors) |
| Emit real signed inventory packet | PASS |
| Verify: constitution + signature + blob | PASS (`SIGNATURE_VALID`) |
| Determinism (fixed captured_utc) | PASS (identical commit_hash & content_ref) |
| Offline (no ATLAS_NFL_URL) | PASS |
| Pledge chain recompute | PASS (7 entries, incl. Feb-flagship seq 1) |
| Ledger tamper detected | PASS (`PLEDGE_HASH_MISMATCH`) |
| Negative vectors | PASS (11/11 rejected, each via its intended rule) |
| Golden vector (pinned content_ref) | PASS |
| Freeze written & self-verifiable | PASS (independently re-checked, 6/6) |

Latest freeze: `proofs/freeze/atlas_tier0_20260831T021107Z`.

## 5. Remaining (post-Tier-0, optional)

- Quarantine the 58 legacy bare-hex packets in `data/outbox` (historical; move to a `_legacy/`
  subfolder). Not done automatically — they are your runtime data.
- Retire/redirect `_RUN_atlas_tier0_packet_verify_v1.ps1` and `_RUN_atlas_emit_inventory_packet_v1.ps1`
  in callers (currently only marked deprecated in the spec).
- Portable golden vector (current commit_hash embeds the machine name via producer_instance;
  content_ref is portable and is what the golden pins).
- Tier-1 items unchanged from the handoff (artifact intake, lineage, query, export/import).

## 6. How to reproduce

Double-click `RUN_ATLAS_FULL.cmd` (or run
`scripts/_RUN_atlas_tier0_signed_green_v1.ps1`). Evidence lands in `proofs/audit/` and, on green,
a sealed freeze in `proofs/freeze/`.

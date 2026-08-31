# ATLAS Tier-0 Specification (v1)

Status: **normative for Tier-0**. This document defines the one canonical
behavior of the Atlas deterministic artifact/commitment/handoff instrument.
Where earlier scripts or packets disagree with this document, this document wins.

## 0. Tier-0 theorem (what "done" means)

> Atlas can independently take an Atlas-native inventory observation, establish a
> deterministic content identity, cryptographically commit a producer to it with an
> Ed25519 signature, preserve a local append-only hash-linked pledge, package the
> commitment under a stable public packet constitution, and allow a separate verifier
> to detect any alteration — all without the cloud or any other ecosystem instrument.

This is proven by `scripts/_RUN_atlas_tier0_signed_green_v1.ps1`, which prints
`ATLAS_TIER0_FULL_GREEN_OK` only after every mandatory positive and negative proof passes,
then writes a self-verifiable freeze under `proofs/freeze/`.

## 1. Canonical laws

1. **Byte law.** Every cryptographic identity derives from exact canonical bytes.
2. **Hash law.** SHA-256, lowercase hex, no ambiguity.
3. **Canonical JSON law.** Objects that participate in identity are serialized with
   `Atlas.Handoff.CanonicalJson`: UTF-8, no BOM, keys sorted ordinally, no insignificant
   whitespace, `\n` newlines only. This is the *single* canonicalizer; verifiers that
   recompute a hash MUST use it (the pledge verifier does so in-process via `verify-pledge`).
4. **Content-address law.** `data/blobs/<hex>` MUST satisfy `sha256(file bytes) == hex`.
   A `content_ref` is `sha256:<hex>`.
5. **No self-reference law.** Option-A `manifest.json` MUST NOT contain a `packet_id` field.
6. **Packet identity law.** `packet_id = sha256(exact on-disk manifest.json bytes)`, and the
   packet directory is named by that hex.
7. **packet_id.txt law.** Contains exactly `sha256:<hex>\n` (LF only, the `sha256:` prefix is
   REQUIRED). Bare hex is INVALID.
8. **Final checksum law.** `sha256sums.txt` is generated last, over final on-disk bytes,
   `<hex><two spaces><rel/path>\n`, LF only, forward-slash paths. It MUST NOT list itself.
9. **Verification law.** Verification never repairs the artifact it inspects; it accepts the
   exact bytes or rejects them.
10. **Append law.** The pledge log is append-only and hash-linked; prior entries are never
    rewritten.
11. **Offline law.** Absence of `ATLAS_NFL_URL` (or any network) cannot invalidate local
    operation; the local outbox is the required fallback.
12. **Evidence law.** A successful operation emits evidence sufficient to verify the claim later.
13. **Failure law.** Invalid state fails closed; it is never normalized into apparent validity.

## 2. Canonical objects

- **Inventory snapshot** — `atlas.inventory.snapshot.v1`
  `{schema, device_id, captured_utc, hostname, os_family, os_version, agent_version, tags[]}`.
  Canonical bytes are content-addressed to a blob; `content_ref = sha256:<hex>`.
- **Commitment** — `commitment.v1`
  `{schema, producer, producer_instance, event_type, event_time_utc, prev_links[], content_ref,
  strength, policy_tags[], notes_ref}`. `commit_hash = sha256(canonical(commitment))`.
  For inventory, `content_ref` binds to the REAL snapshot blob (never the literal "sealed").
- **Signature** — Ed25519 via `ssh-keygen -Y sign`, namespace `atlas-handoff`, principal `atlas`,
  over the ASCII bytes `sha256:<commit_hash_hex>\n`. `producer_key_id = sha256(pubkey_line + "\n")`.
- **Pledge entry** — `atlas.pledge.log.v1`, one NDJSON line in `data/pledge/pledge.ndjson`,
  hash-linked: `local_log_hash = sha256(canonical(entry_without_log_hash) + "\n")`, and
  `local_prev_log_hash` equals the previous entry's `local_log_hash`. Genesis prev is 64 zeroes.
- **Packet** — Packet Constitution v1, Option A. Layout:
  `manifest.json`, `packet_id.txt`, `sha256sums.txt`, `payload/commit.payload.json`,
  `payload/nfl.ingest.json`. `nfl.ingest.v1` carries `commit_hash`, `producer_sig_b64`,
  `producer_key_id`, `payload_mode`, `payload_b64`.

## 3. Strength semantics

- **evidence** — Atlas attests the object represents an observation/captured state.
- **deterministic** — Atlas asserts the object is reproducible under a defined deterministic
  contract. Determinism means: fixed canonical input fields (including `captured_utc`) →
  identical `content_ref` and `commit_hash`. It does NOT mean two observations at different
  times share a hash. SHA-256 being deterministic does not by itself justify `deterministic`.

## 4. The instrument commands (Atlas.HandoffCli)

- `emit-inventory --device X --hostname Y [--os-family] [--os-version] [--agent] [--strength]
  [--tag ...] [--captured-utc ISO8601] [--nfl-url URL]`
  Runs the full chain: snapshot → blob → commitment → Ed25519 signature → hash-linked pledge →
  Option-A packet. Emits `EMIT_OK`, `CONTENT_REF`, `COMMIT_HASH`, `PLEDGE_LOG_HASH`, `PACKET_ID`.
- `verify-blob --ref sha256:<hex>` — confirms a blob exists.
- `verify-pledge [--log path]` — recomputes the whole pledge hash-chain with the canonical
  canonicalizer; fails closed on any seq break, prev-link break, or hash mismatch.

Verification of a packet is `scripts/_VERIFY_atlas_signed_inventory_packet_v1.ps1`
(constitution + Ed25519 signature + content_ref→blob resolution). It is the ONE canonical
packet verifier for signed inventory packets.

## 5. Error contract (fail-closed reason codes)

`ATLAS_SIGVERIFY_FAIL:` `DIRNAME_MISMATCH`, `PACKET_ID_TXT_MISMATCH`, `CR_NOT_ALLOWED_LF_ONLY`,
`MANIFEST_CONTAINS_PACKET_ID_FORBIDDEN`, `SHA256SUMS_MUST_END_WITH_LF`, `SUMS_DUP_PATH`,
`SUMS_SELF_REFERENCE_FORBIDDEN`, `SUMS_MISSING_REQUIRED`, `HASH_MISMATCH`, `MANIFEST_SUM_NE_PACKETID`,
`CONTENT_REF_BAD`, `BLOB_MISSING`, `BLOB_HASH_MISMATCH`, `KEY_ID_MISMATCH`, `SIGNATURE_INVALID`.
Pledge: `PLEDGE_SEQ_BREAK`, `PLEDGE_PREV_LINK_BREAK`, `PLEDGE_HASH_MISMATCH`, `PLEDGE_LOG_MISSING`.

## 6. Isolation boundaries (unchanged, restated)

Tier-0 requires none of NFL, NeverLost, Watchtower, TRIAD, Covenant, cloud, Docker, or network.
Atlas duplicates to NFL only when `ATLAS_NFL_URL` is set; otherwise the local outbox packet is the
handoff. Atlas is a cryptographic signer, not the ecosystem identity/authorization/policy authority.

## 7. Deprecated / superseded (do not use for signed inventory)

- `scripts/_RUN_atlas_tier0_packet_verify_v1.ps1` — accepted **bare-hex** `packet_id.txt` and
  required no commitment/signature. It contradicts laws 7 and the Tier-0 theorem. Superseded by
  `_VERIFY_atlas_signed_inventory_packet_v1.ps1`.
- `scripts/_RUN_atlas_emit_inventory_packet_v1.ps1` — built unsigned pointer-packets that
  self-referenced `sha256sums.txt`. Superseded by the `emit-inventory` CLI command.
- Outbox packets produced before this spec that use bare-hex `packet_id.txt` are legacy and
  do not satisfy the constitution; they are retained only as historical artifacts.

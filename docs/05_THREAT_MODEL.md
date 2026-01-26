# Threat Model

## Assets
- Integrity of installed software
- System stability and availability
- User trust and transparency
- Artifact log integrity (audit history)

## Attacker Models
A1: Network adversary attempting MITM on downloads.
A2: Local malware attempting to hijack update execution.
A3: Malicious package/source attempting supply chain compromise.
A4: User-space attacker attempting privilege escalation via the agent.

## Threats & Mitigations
T1: Download tampering
- Mitigation: TLS + signature verification + hash verification; record provenance.
T2: Engine abuse to execute arbitrary commands
- Mitigation: engine contract prohibits arbitrary execution; allowlist engine actions; built-in only (MVP).
T3: Privilege escalation via agent IPC
- Mitigation: authenticated IPC; role checks; capability gating; explicit elevation prompts.
T4: Log tampering
- Mitigation: append-only artifact directory + hash-chain + restricted permissions.
T5: Dependency confusion / package spoofing
- Mitigation: trusted sources only; restrict community sources by default; source pinning.

## Residual Risk
- Some ecosystems lack consistent signature verification; those engines must degrade safely (NOTIFY only by default) unless user/admin explicitly opts in.

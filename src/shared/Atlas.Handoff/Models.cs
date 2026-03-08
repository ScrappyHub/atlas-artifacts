namespace Atlas.Handoff;

public sealed record CommitmentV1(
    string schema,
    string producer,
    string producer_instance,
    string event_type,
    string event_time_utc,
    string[] prev_links,
    string content_ref,
    string strength,
    string[]? policy_tags,
    string? notes_ref
);

public sealed record PledgeLogEntryV1(
    string schema,
    long local_seq,
    string commit_hash,
    string commit_payload_sha256,
    string producer_sig_b64,
    string producer_key_id,
    string principal,
    string event_type,
    string producer_time_utc,
    string[] prev_links,
    string local_prev_log_hash_sha256,
    string local_log_hash_sha256
);

public sealed record NflIngestV1(
    string schema,
    string commit_hash,
    string producer,
    string producer_sig_b64,
    string producer_key_id,
    string[] prev_links,
    string event_type,
    string producer_time_utc,
    string payload_mode,
    string? payload_b64
);
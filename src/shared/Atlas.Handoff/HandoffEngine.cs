using System.Text;
using System.Text.Json;

namespace Atlas.Handoff;

public sealed class HandoffEngine
{
    private readonly string _producer;
    private readonly string _producerInstance;
    private readonly string _principal;
    private readonly string _dataDir;
    private readonly string _logPath;
    private readonly string _outboxDir;
    private readonly string? _nflUrl;

    public HandoffEngine(string repoRoot, string producer, string producerInstance, string principal, string dataDirRel, string? nflUrl)
    {
        _producer = producer;
        _producerInstance = producerInstance;
        _principal = principal;

        var repo = Path.GetFullPath(repoRoot);
        _dataDir = Path.GetFullPath(Path.Combine(repo, dataDirRel));
        _logPath = Path.Combine(_dataDir, "pledge", "pledge.ndjson");
        _outboxDir = Path.Combine(_dataDir, "outbox");
        _nflUrl = string.IsNullOrWhiteSpace(nflUrl) ? null : nflUrl.Trim();

        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
        Directory.CreateDirectory(_outboxDir);
    }

    public (string commitHashHex, string commitPayloadJsonCanonical, string producerKeyIdSha256, string producerSigB64)
        CommitAndSign(
            string eventType,
            string eventTimeUtc,
            string[] prevLinks,
            string contentRef,
            string strength,
            string[]? policyTags,
            string? notesRef,
            string privateKeyPath,
            string publicKeyLine,
            string sigNamespace = "atlas-handoff"
        )
    {
        var payload = new CommitmentV1(
            schema: "commitment.v1",
            producer: _producer,
            producer_instance: _producerInstance,
            event_type: eventType,
            event_time_utc: eventTimeUtc,
            prev_links: prevLinks ?? Array.Empty<string>(),
            content_ref: contentRef,
            strength: strength,
            policy_tags: policyTags,
            notes_ref: notesRef
        );

        var rawJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
        var canonicalBytes = CanonicalJson.CanonicalizeToUtf8Bytes(rawJson);
        var commitHashHex = Hashing.Sha256Hex(canonicalBytes);

        var keyId = SshKeygenSigner.ComputeKeyIdFromPublicKeyLine(publicKeyLine);
        var signBytes = SshKeygenSigner.BuildSignBytesFromCommitHashHex(commitHashHex);
        var sig = SshKeygenSigner.SignWithSshKeygen(privateKeyPath, _principal, sigNamespace, signBytes);
        var sigB64 = Convert.ToBase64String(sig);

        return (commitHashHex, Encoding.UTF8.GetString(canonicalBytes), "sha256:" + keyId, sigB64);
    }

    public string PledgeLocal(
        string commitHashHex,
        string commitPayloadJsonCanonical,
        string producerSigB64,
        string producerKeyIdSha256,
        string eventType,
        string producerTimeUtc,
        string[] prevLinks
    )
    {
        var commitPayloadSha = "sha256:" + Hashing.Sha256Hex(Encoding.UTF8.GetBytes(commitPayloadJsonCanonical));
        var (seq, prevLogHash) = ReadLastLogState();

        // hash entry canonical bytes + "\n"
        var entryNoHash = new
        {
            schema = "atlas.pledge.log.v1",
            local_seq = seq + 1,
            commit_hash = "sha256:" + commitHashHex,
            commit_payload_sha256 = commitPayloadSha,
            producer_sig_b64 = producerSigB64,
            producer_key_id = producerKeyIdSha256,
            principal = _principal,
            event_type = eventType,
            producer_time_utc = producerTimeUtc,
            prev_links = prevLinks ?? Array.Empty<string>(),
            local_prev_log_hash_sha256 = prevLogHash
        };

        var noHashCanonical = CanonicalJson.CanonicalizeToUtf8Bytes(JsonSerializer.Serialize(entryNoHash));
        var logHashHex = Hashing.Sha256Hex(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(noHashCanonical) + "\n"));

        var full = new PledgeLogEntryV1(
            schema: "atlas.pledge.log.v1",
            local_seq: seq + 1,
            commit_hash: "sha256:" + commitHashHex,
            commit_payload_sha256: commitPayloadSha,
            producer_sig_b64: producerSigB64,
            producer_key_id: producerKeyIdSha256,
            principal: _principal,
            event_type: eventType,
            producer_time_utc: producerTimeUtc,
            prev_links: prevLinks ?? Array.Empty<string>(),
            local_prev_log_hash_sha256: prevLogHash,
            local_log_hash_sha256: "sha256:" + logHashHex
        );

        var line = Encoding.UTF8.GetString(CanonicalJson.CanonicalizeToUtf8Bytes(JsonSerializer.Serialize(full))) + "\n";
        File.AppendAllText(_logPath, line, new UTF8Encoding(false));

        return "sha256:" + logHashHex;
    }

    public string DuplicateToNflOrQueueOutbox(
        string commitHashHex,
        string commitPayloadJsonCanonical,
        string producerSigB64,
        string producerKeyIdSha256,
        string eventType,
        string producerTimeUtc,
        string[] prevLinks,
        string payloadMode // omitted|plaintext|sealed
    )
    {
        // v1: NFL online is optional; if not configured, ALWAYS queue outbox packet (Option A).
        return QueueOutboxPacket_PacketConstitutionV1_OptionA(
            commitHashHex, commitPayloadJsonCanonical, producerSigB64, producerKeyIdSha256, eventType, producerTimeUtc, prevLinks, payloadMode
        );
    }

    // ============================================================
    // Packet Constitution v1 â€” Option A (LOCKED)
    // - manifest.json MUST NOT contain packet_id
    // - packet_id.txt contains PacketId
    // - PacketId = SHA-256(canonical_bytes(manifest-without-id)) == sha256(manifest.json bytes)
    // - sha256sums.txt generated LAST over final on-disk bytes, LF-only
    // Finalization order:
    //   1) payload/**
    //   2) manifest.json (no packet_id) canonical bytes
    //   3) signatures/** (optional later; empty ok)
    //   4) compute PacketId from manifest bytes
    //   5) write packet_id.txt
    //   6) sha256sums.txt LAST
    // ============================================================
    private string QueueOutboxPacket_PacketConstitutionV1_OptionA(
        string commitHashHex,
        string commitPayloadJsonCanonical,
        string producerSigB64,
        string producerKeyIdSha256,
        string eventType,
        string producerTimeUtc,
        string[] prevLinks,
        string payloadMode
    )
    {
        var payloadB64 = payloadMode == "omitted" ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(commitPayloadJsonCanonical));

        var ingest = new NflIngestV1(
            schema: "nfl.ingest.v1",
            commit_hash: "sha256:" + commitHashHex,
            producer: _producer,
            producer_sig_b64: producerSigB64,
            producer_key_id: producerKeyIdSha256,
            prev_links: prevLinks ?? Array.Empty<string>(),
            event_type: eventType,
            producer_time_utc: producerTimeUtc,
            payload_mode: payloadMode,
            payload_b64: payloadB64
        );

        var ingestCanonical = CanonicalJson.CanonicalizeToUtf8Bytes(JsonSerializer.Serialize(ingest));

        var stage = Path.Combine(_outboxDir, ".staging_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(stage);
        Directory.CreateDirectory(Path.Combine(stage, "payload"));
        Directory.CreateDirectory(Path.Combine(stage, "signatures"));

        // 1) payload/**
        var relIngest = "payload/nfl.ingest.json";
        var relCommit = "payload/commit.payload.json";

        File.WriteAllBytes(Path.Combine(stage, "payload", "nfl.ingest.json"), ingestCanonical);
        File.WriteAllBytes(Path.Combine(stage, "payload", "commit.payload.json"), Encoding.UTF8.GetBytes(commitPayloadJsonCanonical));

        // 2) manifest.json (no packet_id)
        var manifestObj = new
        {
            schema = "packet.manifest.v1",
            created_utc = DateTime.UtcNow.ToString("O"),
            producer = _producer,
            kind = "nfl.ingest.v1",
            files = new[] { new { path = relIngest }, new { path = relCommit } }
        };

        var manifestBytes = CanonicalJson.CanonicalizeToUtf8Bytes(JsonSerializer.Serialize(manifestObj));
        var manifestPath = Path.Combine(stage, "manifest.json");
        File.WriteAllBytes(manifestPath, manifestBytes);

        // 3) signatures/** (none in v1)
        // 4) PacketId from manifest bytes
        var packetIdHex = Hashing.Sha256Hex(manifestBytes);

        // 5) packet_id.txt LF-only
        var packetIdPath = Path.Combine(stage, "packet_id.txt");
        File.WriteAllBytes(packetIdPath, Encoding.UTF8.GetBytes("sha256:" + packetIdHex + "\n"));

        // 6) sha256sums.txt LAST from final on-disk bytes
        var sums = new List<string>();
        string SumLine(string hex, string rel) => hex + "  " + rel;

        var manDisk = File.ReadAllBytes(manifestPath);
        var pidDisk = File.ReadAllBytes(packetIdPath);
        var ingDisk = File.ReadAllBytes(Path.Combine(stage, "payload", "nfl.ingest.json"));
        var comDisk = File.ReadAllBytes(Path.Combine(stage, "payload", "commit.payload.json"));

        sums.Add(SumLine(Hashing.Sha256Hex(manDisk), "manifest.json"));
        sums.Add(SumLine(Hashing.Sha256Hex(pidDisk), "packet_id.txt"));
        sums.Add(SumLine(Hashing.Sha256Hex(ingDisk), relIngest));
        sums.Add(SumLine(Hashing.Sha256Hex(comDisk), relCommit));

        var shaText = string.Join("\n", sums) + "\n";
        File.WriteAllBytes(Path.Combine(stage, "sha256sums.txt"), Encoding.UTF8.GetBytes(shaText));

        // finalize directory name = PacketId hex
        var finalDir = Path.Combine(_outboxDir, packetIdHex);
        if (Directory.Exists(finalDir))
            throw new InvalidOperationException("OUTBOX_PACKET_ALREADY_EXISTS: " + finalDir);

        Directory.Move(stage, finalDir);
        return "sha256:" + packetIdHex;
    }

    private (long seq, string prevLogHash) ReadLastLogState()
    {
        if (!File.Exists(_logPath))
            return (0, "sha256:" + new string('0', 64));

        var lines = File.ReadAllLines(_logPath, new UTF8Encoding(false));
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = (lines[i] ?? "").Trim();
            if (line.Length == 0) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var seq = root.GetProperty("local_seq").GetInt64();
            var prev = root.GetProperty("local_log_hash_sha256").GetString();
            if (string.IsNullOrWhiteSpace(prev)) prev = "sha256:" + new string('0', 64);

            return (seq, prev);
        }

        return (0, "sha256:" + new string('0', 64));
    }
}
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Atlas.Handoff;

internal static class Program
{
  // Tier-0 constitutional constants for the dev signing identity.
  private const string Principal = "atlas";
  private const string SigNamespace = "atlas-handoff";
  private const string Producer = "atlas-artifact";

  private static int Main(string[] args)
  {
    if (args == null || args.Length < 1)
    {
      Console.Error.WriteLine("USAGE_FAIL: missing command");
      Console.Error.WriteLine("USAGE: Atlas.HandoffCli <command> [args]");
      Console.Error.WriteLine("  emit-inventory --device X --hostname Y [--os-family windows] [--os-version 10.0] [--agent atlas-cli-dev] [--strength evidence] [--tag lab] [--captured-utc ISO8601] [--nfl-url URL]");
      Console.Error.WriteLine("  verify-blob --ref sha256:<hex>");
      return 2;
    }

    var cmd = args[0].Trim().ToLowerInvariant();
    try
    {
      if (cmd == "emit-inventory") return EmitInventory(args);
      if (cmd == "verify-blob") return VerifyBlob(args);
      if (cmd == "verify-pledge") return VerifyPledge(args);
      Console.Error.WriteLine("UNKNOWN_CMD: " + cmd);
      return 2;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine("CMD_FAIL: " + cmd);
      Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
      return 1;
    }
  }

  private static int VerifyBlob(string[] args)
  {
    string contentRef = "";
    for (int i = 1; i < args.Length; i++)
    {
      var a = args[i];
      string Next()
      {
        if (i + 1 >= args.Length) throw new InvalidOperationException("Missing value after " + a);
        i++;
        return args[i];
      }
      if (a == "--ref") contentRef = Next();
      else throw new InvalidOperationException("Unknown arg: " + a);
    }
    if (string.IsNullOrWhiteSpace(contentRef)) throw new InvalidOperationException("--ref required");

    var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    var blobPath = BlobStore.PathForContentRef(repoRoot, contentRef);
    if (!File.Exists(blobPath))
    {
      Console.Error.WriteLine("BLOB_MISSING");
      Console.Error.WriteLine("BLOB_PATH=" + blobPath);
      return 3;
    }
    var fi = new FileInfo(blobPath);
    Console.WriteLine("VERIFY_BLOB_OK");
    Console.WriteLine("CONTENT_REF=" + contentRef);
    Console.WriteLine("BLOB_PATH=" + blobPath);
    Console.WriteLine("BLOB_LEN=" + fi.Length);
    return 0;
  }

  private static int EmitInventory(string[] args)
  {
    string deviceId = "";
    string hostname = "";
    string osFamily = "unknown";
    string osVersion = "unknown";
    string agent = "atlas-cli-dev";
    string strength = "evidence";
    string? capturedUtcOverride = null;
    string? nflUrl = null;
    var tags = new List<string>();

    for (int i = 1; i < args.Length; i++)
    {
      var a = args[i];
      string Next()
      {
        if (i + 1 >= args.Length) throw new InvalidOperationException("Missing value after " + a);
        i++;
        return args[i];
      }

      if (a == "--device") deviceId = Next();
      else if (a == "--hostname") hostname = Next();
      else if (a == "--os-family") osFamily = Next();
      else if (a == "--os-version") osVersion = Next();
      else if (a == "--agent") agent = Next();
      else if (a == "--strength") strength = Next();
      else if (a == "--tag") tags.Add(Next());
      else if (a == "--captured-utc") capturedUtcOverride = Next();
      else if (a == "--nfl-url") nflUrl = Next();
      else throw new InvalidOperationException("Unknown arg: " + a);
    }

    if (string.IsNullOrWhiteSpace(deviceId)) throw new InvalidOperationException("--device required");
    if (string.IsNullOrWhiteSpace(hostname)) throw new InvalidOperationException("--hostname required");

    var capturedUtc = string.IsNullOrWhiteSpace(capturedUtcOverride)
      ? DateTime.UtcNow.ToString("O")
      : capturedUtcOverride.Trim();

    // Stage 1-2: canonical inventory snapshot payload
    var payloadObj = new AtlasInventorySnapshotV1(
      schema: "atlas.inventory.snapshot.v1",
      device_id: deviceId,
      captured_utc: capturedUtc,
      hostname: hostname,
      os_family: osFamily,
      os_version: osVersion,
      agent_version: agent,
      tags: tags.ToArray()
    );
    var payloadBytes = AtlasPayloads.CanonicalBytes_InventorySnapshot(payloadObj);

    var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());

    // Stage 3: content-address the actual inventory bytes
    var contentRef = BlobStore.PutBlob(repoRoot, payloadBytes);
    var blobPath = BlobStore.PathForContentRef(repoRoot, contentRef);

    // Signing material
    var privKeyPath = Path.Combine(repoRoot, "keys", "atlas-dev-ed25519");
    var pubKeyPath = Path.Combine(repoRoot, "keys", "atlas-dev-ed25519.pub");
    if (!File.Exists(privKeyPath)) throw new FileNotFoundException("missing signing key", privKeyPath);
    if (!File.Exists(pubKeyPath)) throw new FileNotFoundException("missing public key", pubKeyPath);
    var pubLine = File.ReadAllText(pubKeyPath).Trim();

    var producerInstance = Environment.MachineName + "-" + deviceId;
    var engine = new HandoffEngine(
      repoRoot: repoRoot,
      producer: Producer,
      producerInstance: producerInstance,
      principal: Principal,
      dataDirRel: "data",
      nflUrl: nflUrl
    );

    var eventType = "atlas.inventory.snapshot.v1";
    var eventTime = capturedUtc; // bind commitment time to the observation time (determinism-friendly)
    var prevLinks = Array.Empty<string>();

    // Stage 4-5: commitment + Ed25519 signature (binds producer to the REAL content_ref)
    var (commitHashHex, commitCanonical, keyId, sigB64) = engine.CommitAndSign(
      eventType: eventType,
      eventTimeUtc: eventTime,
      prevLinks: prevLinks,
      contentRef: contentRef,
      strength: strength,
      policyTags: new[] { "atlas", "standalone" },
      notesRef: null,
      privateKeyPath: privKeyPath,
      publicKeyLine: pubLine,
      sigNamespace: SigNamespace
    );

    // Stage 6-7: local append-only hash-linked pledge
    var pledgeLogHash = engine.PledgeLocal(
      commitHashHex: commitHashHex,
      commitPayloadJsonCanonical: commitCanonical,
      producerSigB64: sigB64,
      producerKeyIdSha256: keyId,
      eventType: eventType,
      producerTimeUtc: eventTime,
      prevLinks: prevLinks
    );

    // Stage 8: Option-A packet (NFL if configured, else local outbox)
    var packetId = engine.DuplicateToNflOrQueueOutbox(
      commitHashHex: commitHashHex,
      commitPayloadJsonCanonical: commitCanonical,
      producerSigB64: sigB64,
      producerKeyIdSha256: keyId,
      eventType: eventType,
      producerTimeUtc: eventTime,
      prevLinks: prevLinks,
      payloadMode: "plaintext"
    );

    Console.WriteLine("EMIT_OK");
    Console.WriteLine("CONTENT_REF=" + contentRef);
    Console.WriteLine("BLOB_PATH=" + blobPath);
    Console.WriteLine("COMMIT_HASH=sha256:" + commitHashHex);
    Console.WriteLine("PRODUCER_KEY_ID=" + keyId);
    Console.WriteLine("PLEDGE_LOG_HASH=" + pledgeLogHash);
    Console.WriteLine("PACKET_ID=" + packetId);
    Console.WriteLine("CAPTURED_UTC=" + capturedUtc);
    Console.WriteLine("DEVICE_ID=" + deviceId);
    return 0;
  }

  private static int VerifyPledge(string[] args)
  {
    string? logPath = null;
    for (int i = 1; i < args.Length; i++)
    {
      var a = args[i];
      string Next()
      {
        if (i + 1 >= args.Length) throw new InvalidOperationException("Missing value after " + a);
        i++;
        return args[i];
      }
      if (a == "--log") logPath = Next();
      else throw new InvalidOperationException("Unknown arg: " + a);
    }

    var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    logPath ??= Path.Combine(repoRoot, "data", "pledge", "pledge.ndjson");
    if (!File.Exists(logPath))
    {
      Console.Error.WriteLine("PLEDGE_LOG_MISSING: " + logPath);
      return 3;
    }

    var lines = File.ReadAllLines(logPath, new UTF8Encoding(false));
    long expectedSeq = 0;
    string prev = "sha256:" + new string('0', 64);
    long entries = 0;

    foreach (var raw in lines)
    {
      var line = (raw ?? "").Trim();
      if (line.Length == 0) continue;

      using var doc = JsonDocument.Parse(line);
      var r = doc.RootElement;

      var schema = r.GetProperty("schema").GetString();
      var seq = r.GetProperty("local_seq").GetInt64();
      var commitHash = r.GetProperty("commit_hash").GetString();
      var commitPayloadSha = r.GetProperty("commit_payload_sha256").GetString();
      var sig = r.GetProperty("producer_sig_b64").GetString();
      var keyId = r.GetProperty("producer_key_id").GetString();
      var principal = r.GetProperty("principal").GetString();
      var eventType = r.GetProperty("event_type").GetString();
      var producerTime = r.GetProperty("producer_time_utc").GetString();
      var prevLinks = r.GetProperty("prev_links").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
      var prevLog = r.GetProperty("local_prev_log_hash_sha256").GetString();
      var storedLogHash = r.GetProperty("local_log_hash_sha256").GetString();

      if (seq != expectedSeq + 1)
      {
        Console.Error.WriteLine("PLEDGE_SEQ_BREAK at seq=" + seq + " expected=" + (expectedSeq + 1));
        return 4;
      }
      if (prevLog != prev)
      {
        Console.Error.WriteLine("PLEDGE_PREV_LINK_BREAK at seq=" + seq);
        return 4;
      }

      var entryNoHash = new
      {
        schema = schema,
        local_seq = seq,
        commit_hash = commitHash,
        commit_payload_sha256 = commitPayloadSha,
        producer_sig_b64 = sig,
        producer_key_id = keyId,
        principal = principal,
        event_type = eventType,
        producer_time_utc = producerTime,
        prev_links = prevLinks,
        local_prev_log_hash_sha256 = prevLog
      };

      var canon = CanonicalJson.CanonicalizeToUtf8Bytes(JsonSerializer.Serialize(entryNoHash));
      var recomputed = "sha256:" + Hashing.Sha256Hex(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(canon) + "\n"));
      if (recomputed != storedLogHash)
      {
        Console.Error.WriteLine("PLEDGE_HASH_MISMATCH at seq=" + seq + " stored=" + storedLogHash + " recomputed=" + recomputed);
        return 4;
      }

      expectedSeq = seq;
      prev = storedLogHash ?? prev;
      entries++;
    }

    Console.WriteLine("PLEDGE_VERIFY_OK");
    Console.WriteLine("ENTRIES=" + entries);
    Console.WriteLine("HEAD_LOG_HASH=" + prev);
    return 0;
  }
}

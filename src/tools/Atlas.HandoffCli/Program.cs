using System;
using System.IO;
using System.Collections.Generic;
using Atlas.Handoff;

internal static class Program
{
  private static int Main(string[] args)
  {
    if (args == null || args.Length < 1)
    {
      Console.Error.WriteLine("USAGE_FAIL: missing command");
      Console.Error.WriteLine("USAGE: Atlas.HandoffCli <command> [args]");
      Console.Error.WriteLine("  emit-inventory --device X --hostname Y [--os-family windows] [--os-version 10.0] [--agent atlas-cli-dev] [--strength evidence] [--tag lab]");
      Console.Error.WriteLine("  verify-blob --ref sha256:<hex>");
      return 2;
    }

    var cmd = args[0].Trim().ToLowerInvariant();
    try
    {
      if (cmd == "emit-inventory") return EmitInventory(args);
      if (cmd == "verify-blob") return VerifyBlob(args);
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
      else throw new InvalidOperationException("Unknown arg: " + a);
    }

    if (string.IsNullOrWhiteSpace(deviceId)) throw new InvalidOperationException("--device required");
    if (string.IsNullOrWhiteSpace(hostname)) throw new InvalidOperationException("--hostname required");

    var nowUtc = DateTime.UtcNow.ToString("O");
    var payloadObj = new AtlasInventorySnapshotV1(
      schema: "atlas.inventory.snapshot.v1",
      device_id: deviceId,
      captured_utc: nowUtc,
      hostname: hostname,
      os_family: osFamily,
      os_version: osVersion,
      agent_version: agent,
      tags: tags.ToArray()
    );

    var payloadBytes = AtlasPayloads.CanonicalBytes_InventorySnapshot(payloadObj);
    var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    var contentRef = BlobStore.PutBlob(repoRoot, payloadBytes);
    var blobPath = BlobStore.PathForContentRef(repoRoot, contentRef);

    Console.WriteLine("EMIT_OK");
    Console.WriteLine("CONTENT_REF=" + contentRef);
    Console.WriteLine("BLOB_PATH=" + blobPath);
    Console.WriteLine("CAPTURED_UTC=" + nowUtc);
    Console.WriteLine("DEVICE_ID=" + deviceId);
    return 0;
  }
}

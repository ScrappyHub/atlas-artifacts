using System;
using System.Collections.Generic;

namespace Atlas.Agent.Engines.Rollback;

public sealed record RollbackManifest(
    string RunId,
    DateTimeOffset CreatedAtUtc,
    string SourceDir,
    string SnapshotZip,
    IReadOnlyList<RollbackFileEntry> Files
);

public sealed record RollbackFileEntry(
    string RelativePath,
    long SizeBytes,
    string Sha256Hex
);

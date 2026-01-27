using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Atlas.Agent.Engines.Rollback;

public sealed class RollbackEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates a snapshot zip + manifest under:
    ///   runs/<runId>/snapshot/snapshot.zip
    ///   runs/<runId>/snapshot/manifest.json
    /// Returns the manifest path.
    /// </summary>
    public string CreateSnapshot(string runId, string sourceDir, string runsRoot)
    {
        if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("runId required", nameof(runId));
        if (string.IsNullOrWhiteSpace(sourceDir)) throw new ArgumentException("sourceDir required", nameof(sourceDir));
        if (string.IsNullOrWhiteSpace(runsRoot)) throw new ArgumentException("runsRoot required", nameof(runsRoot));

        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        var snapDir = Path.Combine(runsRoot, runId, "snapshot");
        Directory.CreateDirectory(snapDir);

        var zipPath = Path.Combine(snapDir, "snapshot.zip");
        var manifestPath = Path.Combine(snapDir, "manifest.json");

        if (File.Exists(zipPath)) File.Delete(zipPath);
        if (File.Exists(manifestPath)) File.Delete(manifestPath);

        // Build manifest entries from sourceDir
        var files = new List<RollbackFileEntry>();
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
            var fi = new FileInfo(file);
            var sha = Sha256Hex(File.ReadAllBytes(file));
            files.Add(new RollbackFileEntry(rel, fi.Length, sha));
        }

        // Zip the directory
        ZipFile.CreateFromDirectory(
            sourceDir,
            zipPath,
            CompressionLevel.Optimal,
            includeBaseDirectory: false
        );

        var manifest = new RollbackManifest(
            RunId: runId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            SourceDir: sourceDir,
            SnapshotZip: zipPath,
            Files: files
        );

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
        return manifestPath;
    }

    /// <summary>
    /// Restores snapshot.zip into targetDir.
    /// Safe extraction: rejects zip entries with path traversal.
    /// </summary>
    public void Restore(string snapshotZipPath, string targetDir)
    {
        if (string.IsNullOrWhiteSpace(snapshotZipPath)) throw new ArgumentException("snapshotZipPath required", nameof(snapshotZipPath));
        if (string.IsNullOrWhiteSpace(targetDir)) throw new ArgumentException("targetDir required", nameof(targetDir));
        if (!File.Exists(snapshotZipPath)) throw new FileNotFoundException("Snapshot zip not found", snapshotZipPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "atlas_restore_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);

        try
        {
            using var archive = ZipFile.OpenRead(snapshotZipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.FullName))
                    continue;

                // Normalize to OS path, then validate it stays inside tempDir
                var destPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));
                var tempRoot = Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar;

                if (!destPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Unsafe zip entry path traversal: {entry.FullName}");

                // Directory entry
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                    entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(destPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, overwrite: true);
            }

            // Replace targetDir contents
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);

            Directory.CreateDirectory(targetDir);

            // Copy extracted tempDir -> targetDir
            foreach (var dir in Directory.EnumerateDirectories(tempDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(tempDir, dir);
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            foreach (var file in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(tempDir, file);
                var dst = Path.Combine(targetDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(file, dst, overwrite: true);
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

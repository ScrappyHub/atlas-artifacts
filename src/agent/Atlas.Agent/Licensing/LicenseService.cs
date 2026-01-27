using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Atlas.Agent.Licensing;

public sealed class LicenseService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    public License Current { get; private set; } = License.Unlicensed();
    public bool IsValid { get; private set; }
    public string? Error { get; private set; }

    /// <summary>
    /// Loads + verifies license from canonical disk paths:
    ///   AppPaths.LicenseJsonPath
    ///   AppPaths.LicenseSigPath
    /// </summary>
    public void Load()
    {
        IsValid = false;
        Error = null;
        Current = License.Unlicensed();

        if (!File.Exists(AppPaths.LicenseJsonPath) || !File.Exists(AppPaths.LicenseSigPath))
        {
            Error = "License files not found.";
            return;
        }

        byte[] jsonBytes;
        string sigText;

        try
        {
            jsonBytes = File.ReadAllBytes(AppPaths.LicenseJsonPath);
            sigText = File.ReadAllText(AppPaths.LicenseSigPath);
        }
        catch (Exception ex)
        {
            Error = $"Failed to read license files: {ex.Message}";
            return;
        }

        if (!LicenseVerifier.Verify(jsonBytes, sigText))
        {
            Error = "License signature verification failed.";
            return;
        }

        LicensePayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(jsonBytes, JsonOpts)
                      ?? throw new InvalidOperationException("License payload is null.");
        }
        catch (Exception ex)
        {
            Error = $"License JSON parse failed: {ex.Message}";
            return;
        }

        // Basic field validation
        if (string.IsNullOrWhiteSpace(payload.LicenseId) ||
            string.IsNullOrWhiteSpace(payload.Customer) ||
            string.IsNullOrWhiteSpace(payload.Tier) ||
            payload.DeviceLimit < 1 ||
            payload.Features is null ||
            payload.Features.Count < 1)
        {
            Error = "License payload validation failed (required fields).";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (payload.ExpiresAt.HasValue && now > payload.ExpiresAt.Value)
        {
            Error = "License is expired.";
            return;
        }

        // Convert features strings -> flags (canonical mapping)
        var flags = Feature.None;
        foreach (var f in payload.Features.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            flags |= f.Trim() switch
            {
                "winget_scan" => Feature.WingetScan,
                "artifacts"   => Feature.Artifacts,
                "rollback"    => Feature.Rollback,
                "export_logs" => Feature.ExportLogs,
                "automation"  => Feature.Automation,
                _             => Feature.None // unknown feature string is ignored (safe)
            };
        }

        Current = new License(
            LicenseId: payload.LicenseId,
            Customer: payload.Customer,
            Tier: payload.Tier,
            DeviceLimit: payload.DeviceLimit,
            Features: flags,
            IssuedAt: payload.IssuedAt,
            ExpiresAt: payload.ExpiresAt
        );

        IsValid = true;
    }

    public bool HasFeature(Feature f) => IsValid && Current.HasFeature(f);

    public void Require(Feature f)
    {
        if (!HasFeature(f))
            throw new InvalidOperationException($"Feature not licensed: {f}");
    }
}

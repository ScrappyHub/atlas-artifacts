<<<<<<< HEAD
namespace Atlas.Agent.Engines.Winget;

public sealed record InventoryItem(
    string app_key,
    string display_name,
    string version,
    string source,
    string? vendor
);

public sealed record InventorySnapshot(
    string device_id,
    string captured_at,
    List<InventoryItem> items
);

public sealed record CandidateItem(
    string app_key,
    string engine_id,
    string package_id,
    string current_version,
    string candidate_version,
    bool requires_admin,
    string source_type,
    string source_id,
    object? evidence
);

public sealed record CandidatesSnapshot(
    string run_id,
    string captured_at,
    List<CandidateItem> candidates
);
=======
namespace Atlas.Agent.Engines.Winget;

public sealed record InventoryItem(string app_key, string display_name, string version, string source, string? vendor);

public sealed record InventorySnapshot(string device_id, string captured_at, List<InventoryItem> items);

public sealed record CandidateItem(
    string app_key,
    string engine_id,
    string package_id,
    string current_version,
    string candidate_version,
    bool requires_admin,
    string source_type,
    string source_id,
    object? evidence
);

public sealed record CandidatesSnapshot(string run_id, string captured_at, List<CandidateItem> candidates);
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

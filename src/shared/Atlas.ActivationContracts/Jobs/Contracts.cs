namespace Atlas.ActivationContracts;

// Keep enums stable (wire contract).
public enum AtlasJobType
{
    Inventory = 0,
    Download = 1,
    Install = 2,
    Stop = 3,
    Restore = 4
}

public enum AtlasJobState
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Canceled = 4
}

public record CreateJobRequest(
    string TenantId,
    string DeviceId,
    AtlasJobType Type,
    string PayloadJson
);

public record CreateJobResponse(
    bool Ok,
    string Code,
    AtlasJob? Job
);

public record AtlasJob(
    string JobId,
    string TenantId,
    string DeviceId,
    AtlasJobType Type,
    AtlasJobState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ScheduledForUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string PayloadJson,
    string? ResultJson,
    bool CancelRequested
);

public record PollJobsResponse(
    bool Ok,
    string Code,
    AtlasJob[] Jobs
);

public record CancelJobResponse(
    bool Ok,
    string Code
);

public record StartJobResponse(
    bool Ok,
    string Code
);

public record CancelAckResponse(
    bool Ok,
    string Code
);

public record GetJobResponse(
    bool Ok,
    string Code,
    AtlasJob? Job
);

public record CompleteJobRequest(
    string TenantId,
    string DeviceId,
    string JobId,
    bool Ok,
    string ResultJson
);

public record CompleteJobResponse(
    bool Ok,
    string Code
);

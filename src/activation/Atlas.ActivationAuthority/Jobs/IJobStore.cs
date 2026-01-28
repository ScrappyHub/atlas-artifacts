using Atlas.ActivationContracts;

namespace Atlas.ActivationAuthority.Jobs;

public readonly record struct JobOpResult(bool Ok, string Code);

public interface IJobStore
{
    Task<AtlasJob> CreateAsync(CreateJobRequest req, CancellationToken ct);

    // Poll returns ONLY:
    // - cancel directives first: CancelRequested=true && !terminal
    // - then runnable: State=Queued && CancelRequested=false
    // Terminal jobs MUST NOT be returned.
    Task<IReadOnlyList<AtlasJob>> PollAsync(string tenantId, string deviceId, int max, CancellationToken ct);

    // Debug/reconciliation
    Task<(JobOpResult Result, AtlasJob? Job)> GetAsync(string tenantId, string deviceId, string jobId, CancellationToken ct);

    // Canonical transitions:
    // Queued -> Running only. Idempotent: start on Running returns ok (already_running).
    Task<JobOpResult> StartAsync(string tenantId, string deviceId, string jobId, CancellationToken ct);

    // Canonical transitions:
    // Running -> Succeeded|Failed only. (No completion from Queued.)
    // Disallowed if CancelRequested=true (truth: no falsification).
    Task<JobOpResult> CompleteAsync(string tenantId, string deviceId, string jobId, bool ok, string resultJson, CancellationToken ct);

    // RequestCancel toggles CancelRequested=true (does NOT change State).
    // Idempotent: repeat request returns ok (already_requested).
    Task<JobOpResult> RequestCancelAsync(string tenantId, string deviceId, string jobId, CancellationToken ct);

    // CancelAck is the agent acknowledgement:
    // (Queued|Running) + CancelRequested=true -> Canceled (terminal).
    // Idempotent: repeat ack on already-canceled returns ok (already_canceled).
    Task<JobOpResult> CancelAckAsync(string tenantId, string deviceId, string jobId, CancellationToken ct);
}

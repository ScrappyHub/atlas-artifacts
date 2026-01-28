using System.Collections.Concurrent;
using Atlas.ActivationContracts;

namespace Atlas.ActivationAuthority.Jobs;

public sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, AtlasJob> _jobs = new();

    // Keep terminal jobs for a while for debug/reconciliation; avoids unbounded growth in dev.
    private readonly TimeSpan _retention = TimeSpan.FromHours(24);

    private static string Key(string tenantId, string jobId) => $"{tenantId}::{jobId}";

    private static bool IsTerminal(AtlasJobState s)
        => s == AtlasJobState.Succeeded || s == AtlasJobState.Failed || s == AtlasJobState.Canceled;

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var kv in _jobs)
        {
            var j = kv.Value;
            if (!IsTerminal(j.State)) continue;

            // If EndedAtUtc missing (shouldn't happen), keep it.
            var ended = j.EndedAtUtc;
            if (ended is null) continue;

            if (now - ended.Value > _retention)
                _jobs.TryRemove(kv.Key, out _);
        }
    }

    private bool TryGet(string tenantId, string jobId, out AtlasJob job)
        => _jobs.TryGetValue(Key(tenantId, jobId), out job);

    // Concurrency-safe update (CAS loop) so we don't lose updates if two requests hit same job.
    private JobOpResult Update(
        string tenantId,
        string jobId,
        Func<AtlasJob, (JobOpResult Result, AtlasJob? Updated)> apply)
    {
        while (true)
        {
            if (!TryGet(tenantId, jobId, out var current))
                return new JobOpResult(false, "not_found");

            var (res, updated) = apply(current);
            if (!res.Ok || updated is null)
                return res;

            var k = Key(tenantId, jobId);

            // If someone changed the job between read and write, retry.
            if (_jobs.TryUpdate(k, updated!, current))
                return res;
        }
    }

    public Task<AtlasJob> CreateAsync(CreateJobRequest req, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CleanupExpired();

        var now = DateTimeOffset.UtcNow;
        var jobId = Guid.NewGuid().ToString("N");

        var job = new AtlasJob(
            JobId: jobId,
            TenantId: req.TenantId,
            DeviceId: req.DeviceId,
            Type: req.Type,
            State: AtlasJobState.Queued,
            CreatedAtUtc: now,
            ScheduledForUtc: null,
            StartedAtUtc: null,
            EndedAtUtc: null,
            PayloadJson: string.IsNullOrWhiteSpace(req.PayloadJson) ? "{}" : req.PayloadJson,
            ResultJson: null,
            CancelRequested: false
        );

        _jobs[Key(req.TenantId, jobId)] = job;
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<AtlasJob>> PollAsync(string tenantId, string deviceId, int max, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CleanupExpired();

        max = Math.Max(0, max);

        // 1) Cancels first — cannot be starved by queued jobs.
        var cancels = _jobs.Values
            .Where(j =>
                !IsTerminal(j.State) &&
                string.Equals(j.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(j.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) &&
                j.CancelRequested == true)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(max)
            .ToList();

        var remaining = Math.Max(0, max - cancels.Count);

        // 2) Runnable (Queued only, cancelRequested=false)
        var runnable = remaining == 0
            ? new List<AtlasJob>()
            : _jobs.Values
                .Where(j =>
                    !IsTerminal(j.State) &&
                    string.Equals(j.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(j.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) &&
                    j.State == AtlasJobState.Queued &&
                    j.CancelRequested == false)
                .OrderBy(j => j.CreatedAtUtc)
                .Take(remaining)
                .ToList();

        var merged = cancels
            .Concat(runnable)
            .GroupBy(j => j.JobId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(j => j.CreatedAtUtc)
            .ToList()
            .AsReadOnly();

        return Task.FromResult((IReadOnlyList<AtlasJob>)merged);
    }

    public Task<(JobOpResult Result, AtlasJob? Job)> GetAsync(string tenantId, string deviceId, string jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!TryGet(tenantId, jobId, out var job))
            return Task.FromResult<(JobOpResult Result, AtlasJob? Job)>((new JobOpResult(false, "not_found"), (AtlasJob?)null));

        if (!string.Equals(job.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<(JobOpResult Result, AtlasJob? Job)>((new JobOpResult(false, "forbidden"), (AtlasJob?)null));

        return Task.FromResult<(JobOpResult Result, AtlasJob? Job)>((new JobOpResult(true, "ok"), job));
    }

    public Task<JobOpResult> StartAsync(string tenantId, string deviceId, string jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var r = Update(tenantId, jobId, current =>
        {
            if (!string.Equals(current.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                return (new JobOpResult(false, "forbidden"), null);

            if (IsTerminal(current.State))
                return (new JobOpResult(false, "invalid_state"), null);

            // Idempotent: already Running => ok
            if (current.State == AtlasJobState.Running)
                return (new JobOpResult(true, "already_running"), current);

            // Only Queued -> Running; cancel requested blocks start
            if (current.State != AtlasJobState.Queued || current.CancelRequested)
                return (new JobOpResult(false, "invalid_state"), null);

            var updated = current with
            {
                State = AtlasJobState.Running,
                StartedAtUtc = DateTimeOffset.UtcNow
            };

            return (new JobOpResult(true, "ok"), updated);
        });

        return Task.FromResult(r);
    }

    public Task<JobOpResult> CompleteAsync(string tenantId, string deviceId, string jobId, bool ok, string resultJson, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var r = Update(tenantId, jobId, current =>
        {
            if (!string.Equals(current.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                return (new JobOpResult(false, "forbidden"), null);

            if (IsTerminal(current.State))
                return (new JobOpResult(false, "invalid_state"), null);

            // Truth rule: if cancel requested, completion is disallowed
            if (current.CancelRequested)
                return (new JobOpResult(false, "invalid_state"), null);

            // LOCK: complete ONLY from Running (no Queued completion)
            if (current.State != AtlasJobState.Running)
                return (new JobOpResult(false, "invalid_state"), null);

            var updated = current with
            {
                State = ok ? AtlasJobState.Succeeded : AtlasJobState.Failed,
                EndedAtUtc = DateTimeOffset.UtcNow,
                ResultJson = string.IsNullOrWhiteSpace(resultJson) ? "{}" : resultJson,
                CancelRequested = false
            };

            return (new JobOpResult(true, "ok"), updated);
        });

        return Task.FromResult(r);
    }

    public Task<JobOpResult> RequestCancelAsync(string tenantId, string deviceId, string jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var r = Update(tenantId, jobId, current =>
        {
            if (!string.Equals(current.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                return (new JobOpResult(false, "forbidden"), null);

            if (IsTerminal(current.State))
                return (new JobOpResult(false, "invalid_state"), null);

            // Idempotent: already requested => ok
            if (current.CancelRequested)
                return (new JobOpResult(true, "already_requested"), current);

            var updated = current with { CancelRequested = true };
            return (new JobOpResult(true, "ok"), updated);
        });

        return Task.FromResult(r);
    }

    public Task<JobOpResult> CancelAckAsync(string tenantId, string deviceId, string jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var r = Update(tenantId, jobId, current =>
        {
            if (!string.Equals(current.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                return (new JobOpResult(false, "forbidden"), null);

            // Idempotent: already canceled => ok
            if (current.State == AtlasJobState.Canceled)
                return (new JobOpResult(true, "already_canceled"), current);

            if (IsTerminal(current.State))
                return (new JobOpResult(false, "invalid_state"), null);

            // Must have been requested
            if (!current.CancelRequested)
                return (new JobOpResult(false, "invalid_state"), null);

            // Canonical: (Queued|Running) + CancelRequested=true -> Canceled terminal
            if (current.State != AtlasJobState.Queued && current.State != AtlasJobState.Running)
                return (new JobOpResult(false, "invalid_state"), null);

            var updated = current with
            {
                State = AtlasJobState.Canceled,
                EndedAtUtc = DateTimeOffset.UtcNow,
                CancelRequested = false
            };

            return (new JobOpResult(true, "ok"), updated);
        });

        return Task.FromResult(r);
    }
}




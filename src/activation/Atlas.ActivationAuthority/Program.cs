using Atlas.ActivationContracts;
using Atlas.ActivationAuthority.Activation;
using Atlas.ActivationAuthority.Jobs;

var builder = WebApplication.CreateBuilder(args);

// -------- options (env-driven) --------
var ttlHours = int.TryParse(builder.Configuration["ATLAS_GRANT_TTL_HOURS"], out var ttl) ? ttl : 24;
var signingKey = builder.Configuration["ATLAS_SIGNING_KEY"] ?? "dev-only-change-me";
var deviceLimit = int.TryParse(builder.Configuration["ATLAS_DEVICE_LIMIT"], out var lim) ? lim : 5;

builder.Services.AddSingleton(new ActivationOptions
{
    GrantTtlHours = ttlHours,
    SigningKey = signingKey,
    DeviceLimit = deviceLimit
});

// -------- store selection (memory vs postgres) --------
var storeKind = (builder.Configuration["ATLAS_ACTIVATION_STORE"] ?? "memory")
    .Trim()
    .ToLowerInvariant();

if (storeKind == "postgres")
    builder.Services.AddSingleton<IActivationStore, PostgresActivationStore>();
else
    builder.Services.AddSingleton<IActivationStore, InMemoryActivationStore>();

// -------- jobs store (memory for now) --------
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();

var app = builder.Build();

// uniform helpers (avoid Results.StatusCode overload footguns)
static IResult Forbidden<T>(T payload) => Results.Json(payload, statusCode: 403);
static IResult Conflict<T>(T payload) => Results.Json(payload, statusCode: 409);

app.MapGet("/", () => Results.Ok(new { ok = true, service = "Atlas.ActivationAuthority" }));
app.MapGet("/health", () => Results.Ok(new { ok = true }));

// ---------------- Activation ----------------
app.MapPost("/v1/activation/claim", async (
    ActivationClaimRequest req,
    IActivationStore store,
    ActivationOptions opts,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.TenantId) ||
        string.IsNullOrWhiteSpace(req.CustomerId) ||
        string.IsNullOrWhiteSpace(req.LicenseId) ||
        string.IsNullOrWhiteSpace(req.DeviceId))
        return Results.BadRequest(new ActivationClaimResponse(false, "invalid_request", null));

    var current = await store.CountDevicesForLicenseAsync(req.TenantId, req.LicenseId, ct);
    if (current >= opts.DeviceLimit)
        return Results.BadRequest(new ActivationClaimResponse(false, "device_limit_reached", null));

    await store.RegisterDeviceAsync(
        req.TenantId,
        req.LicenseId,
        req.DeviceId,
        req.HardwareFingerprint ?? "",
        req.AgentVersion ?? "",
        ct);

    var now = DateTimeOffset.UtcNow;
    var exp = now.AddHours(opts.GrantTtlHours);

    var payload = $"{req.TenantId}|{req.CustomerId}|{req.LicenseId}|{req.DeviceId}|{now:O}|{exp:O}";
    var sig = GrantSigner.Sign(opts.SigningKey, payload);

    var grant = new ActivationGrant(
        TenantId: req.TenantId,
        CustomerId: req.CustomerId,
        LicenseId: req.LicenseId,
        DeviceId: req.DeviceId,
        IssuedAtUtc: now,
        ExpiresAtUtc: exp,
        Signature: sig
    );

    return Results.Ok(new ActivationClaimResponse(true, "ok", grant));
});

// ---------------- Jobs ----------------

// POST /v1/jobs
app.MapPost("/v1/jobs", async (CreateJobRequest req, IJobStore jobs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.TenantId) || string.IsNullOrWhiteSpace(req.DeviceId))
        return Results.BadRequest(new CreateJobResponse(false, "invalid_request", null));

    var job = await jobs.CreateAsync(req, ct);
    return Results.Ok(new CreateJobResponse(true, "ok", job));
});

// GET /v1/jobs/{id}?tenantId=...&deviceId=...
app.MapGet("/v1/jobs/{id}", async (string id, string tenantId, string deviceId, IJobStore jobs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new GetJobResponse(false, "invalid_request", null));

    var (r, job) = await jobs.GetAsync(tenantId, deviceId, id, ct);

    return r.Code switch
    {
        "ok" => Results.Ok(new GetJobResponse(true, "ok", job)),
        "not_found" => Results.NotFound(new GetJobResponse(false, "not_found", null)),
        "forbidden" => Forbidden(new GetJobResponse(false, "forbidden", null)),
        _ => Results.BadRequest(new GetJobResponse(false, "error", null))
    };
});

// GET /v1/jobs/poll?tenantId=...&deviceId=...&max=...
app.MapGet("/v1/jobs/poll", async (string tenantId, string deviceId, int? max, IJobStore jobs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(deviceId))
        return Results.BadRequest(new PollJobsResponse(false, "invalid_request", Array.Empty<AtlasJob>()));

    var list = await jobs.PollAsync(tenantId, deviceId, max ?? 5, ct);
    return Results.Ok(new PollJobsResponse(true, "ok", list.ToArray()));
});

// POST /v1/jobs/{id}/start?tenantId=...&deviceId=...
app.MapPost("/v1/jobs/{id}/start", async (string id, string tenantId, string deviceId, IJobStore jobs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new StartJobResponse(false, "invalid_request"));

    var r = await jobs.StartAsync(tenantId, deviceId, id, ct);

    return r.Code switch
    {
        "ok" => Results.Ok(new StartJobResponse(true, "ok")),
        "already_running" => Results.Ok(new StartJobResponse(true, "already_running")),
        "not_found" => Results.NotFound(new StartJobResponse(false, "not_found")),
        "forbidden" => Forbidden(new StartJobResponse(false, "forbidden")),
        "invalid_state" => Conflict(new StartJobResponse(false, "invalid_state")),
        _ => Results.BadRequest(new StartJobResponse(false, "error"))
    };
});

// POST /v1/jobs/complete
app.MapPost("/v1/jobs/complete", async (CompleteJobRequest req, IJobStore jobs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.TenantId) ||
        string.IsNullOrWhiteSpace(req.DeviceId) ||
        string.IsNullOrWhiteSpace(req.JobId))
        return Results.BadRequest(new CompleteJobResponse(false, "invalid_request"));

    var r = await jobs.CompleteAsync(req.TenantId, req.DeviceId, req.JobId, req.Ok, req.ResultJson ?? "{}", ct);

    return r.Code switch
    {
        "ok" => Results.Ok(new CompleteJobResponse(true, "ok")),
        "not_found" => Results.NotFound(new CompleteJobResponse(false, "not_found")),
        "forbidden" => Forbidden(new CompleteJobResponse(false, "forbidden")),
        "invalid_state" => Conflict(new CompleteJobResponse(false, "invalid_state")),
        _ => Results.BadRequest(new CompleteJobResponse(false, "error"))
    };
});

// POST /v1/jobs/{id}/cancel?tenantId=...&deviceId=...
app.MapPost("/v1/jobs/{id}/cancel", async (string id, string tenantId, string deviceId, IJobStore jobs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new CancelJobResponse(false, "invalid_request"));

    var r = await jobs.RequestCancelAsync(tenantId, deviceId, id, ct);

    return r.Code switch
    {
        "ok" => Results.Ok(new CancelJobResponse(true, "ok")),
        "already_requested" => Results.Ok(new CancelJobResponse(true, "already_requested")),
        "not_found" => Results.NotFound(new CancelJobResponse(false, "not_found")),
        "forbidden" => Forbidden(new CancelJobResponse(false, "forbidden")),
        "invalid_state" => Conflict(new CancelJobResponse(false, "invalid_state")),
        _ => Results.BadRequest(new CancelJobResponse(false, "error"))
    };
});

// POST /v1/jobs/{id}/cancel-ack?tenantId=...&deviceId=...
app.MapPost("/v1/jobs/{id}/cancel-ack", async (string id, string tenantId, string deviceId, IJobStore jobs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new CancelAckResponse(false, "invalid_request"));

    var r = await jobs.CancelAckAsync(tenantId, deviceId, id, ct);

    return r.Code switch
    {
        "ok" => Results.Ok(new CancelAckResponse(true, "ok")),
        "already_canceled" => Results.Ok(new CancelAckResponse(true, "already_canceled")),
        "not_found" => Results.NotFound(new CancelAckResponse(false, "not_found")),
        "forbidden" => Forbidden(new CancelAckResponse(false, "forbidden")),
        "invalid_state" => Conflict(new CancelAckResponse(false, "invalid_state")),
        _ => Results.BadRequest(new CancelAckResponse(false, "error"))
    };
});

/* ATLAS_A1_AGENTS_HELLO_BEGIN */

app.MapPost("/v1/agents/hello", () => Results.Ok(new { ok = true }));

/* ATLAS_A1_AGENTS_HELLO_END */


app.Run();

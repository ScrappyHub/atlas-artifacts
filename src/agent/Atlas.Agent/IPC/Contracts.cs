using System.Text.Json;

namespace Atlas.Agent.IPC;

public sealed record IpcRequest(
    string request_id,
    string command,
    string user_id,
    JsonElement? payload
);

public sealed record IpcError(string code, string message);

public sealed record IpcResponse(
    string request_id,
    bool ok,
    IpcError? error,
    object? data
);

public static class JsonOpts
{
    // Canonical: exact JSON property names as provided (no naming policy),
    // compact output (WriteIndented=false) for deterministic signing/artifacts.
    public static readonly JsonSerializerOptions Serializer = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };
}

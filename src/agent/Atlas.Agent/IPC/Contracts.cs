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
    public static readonly JsonSerializerOptions Serializer = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };
}

using System;
using System.Text.Json;

namespace Atlas.Agent.IPC;

// Canonical commands. Keep stable forever.
public enum IpcCommand
{
    Ping = 0,
    GetLicenseStatus = 1,
    ScanWinget = 2
}

// Canonical request envelope.
// Frame payload is JSON serialized IpcRequest.
public sealed record IpcRequest(
    IpcCommand Command,
    string RequestId,
    JsonElement? Payload = null
)
{
    public static IpcRequest New(IpcCommand cmd, JsonElement? payload = null) =>
        new(cmd, Guid.NewGuid().ToString("n"), payload);
}

// Canonical response envelope.
public sealed record IpcResponse(
    string RequestId,
    bool Ok,
    string Message,
    object? Data
)
{
    public static IpcResponse Success(string requestId, string message, object? data = null) =>
        new(requestId, true, message, data);

    public static IpcResponse Fail(string requestId, string message, object? data = null) =>
        new(requestId, false, message, data);
}
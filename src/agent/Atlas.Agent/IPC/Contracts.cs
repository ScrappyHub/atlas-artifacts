using System;
using System.Collections.Generic;

namespace Atlas.Agent.IPC;

public enum IpcCommand
{
    Ping = 0,
    ScanWinget = 10
}

public sealed record IpcRequest(
    IpcCommand Command,
    Dictionary<string, string>? Args = null,
    string? RequestId = null);

public sealed record IpcResponse(
    bool Ok,
    string? Message = null,
    object? Data = null,
    string? RequestId = null,
    DateTimeOffset? Timestamp = null);
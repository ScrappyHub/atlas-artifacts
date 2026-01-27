namespace Atlas.Agent.Engines.Winget;

public sealed record WingetPackage(
    string Id,
    string Name,
    string Version,
    string? Source = null);
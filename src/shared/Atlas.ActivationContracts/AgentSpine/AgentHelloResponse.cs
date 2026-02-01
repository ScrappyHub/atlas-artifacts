namespace Atlas.ActivationContracts.AgentSpine;

public sealed record AgentHelloResponse(
    bool ok,
    string code,
    string? message = null
);

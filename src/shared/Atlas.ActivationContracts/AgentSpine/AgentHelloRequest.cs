namespace Atlas.ActivationContracts.AgentSpine;

public sealed record AgentHelloRequest(
    string tenantId,
    string deviceId,
    string os,
    string arch,
    string agentVersion,
    string[] capabilities
);

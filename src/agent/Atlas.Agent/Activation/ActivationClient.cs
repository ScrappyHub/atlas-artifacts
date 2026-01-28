using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
// NOTE: add project reference to Atlas.ActivationContracts when you wire this in:
// dotnet add .\src\agent\Atlas.Agent\Atlas.Agent.csproj reference .\src\shared\Atlas.ActivationContracts\Atlas.ActivationContracts.csproj
using Atlas.ActivationContracts;

namespace Atlas.Agent.Activation;

public sealed class ActivationClient
{
    private readonly HttpClient _http;

    public ActivationClient(HttpClient http) => _http = http;

    public async Task<ActivationClaimResponse> ClaimAsync(ActivationClaimRequest req, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("/v1/activation/claim", req, ct);
        var body = await resp.Content.ReadFromJsonAsync<ActivationClaimResponse>(cancellationToken: ct);
        return body ?? new ActivationClaimResponse(false, "invalid_response", null);
    }
}
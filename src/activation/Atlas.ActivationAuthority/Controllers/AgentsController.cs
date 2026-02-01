using Microsoft.AspNetCore.Mvc;
using Atlas.ActivationContracts.AgentSpine;

namespace Atlas.ActivationAuthority.Controllers;

[ApiController]
public sealed class AgentsController : ControllerBase
{
    [HttpPost("/v1/agents/hello")]
    public ActionResult<AgentHelloResponse> Hello([FromBody] AgentHelloRequest req)
    {
        return Ok(new AgentHelloResponse(ok: true, code: "ok"));
    }
}
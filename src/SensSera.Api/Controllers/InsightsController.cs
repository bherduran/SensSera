using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/insights")]
[Authorize]
public sealed class InsightsController(IInsightService insights) : ControllerBase
{
    // Natural-language question over the caller's own data. Per-tenant rate-limited (LLM cost).
    [HttpPost("ask")]
    [EnableRateLimiting("insights")]
    public async Task<ActionResult<AskResponse>> Ask(
        [FromBody] AskRequest request, CancellationToken cancellationToken)
        => Ok(await insights.AskAsync(request, cancellationToken));
}

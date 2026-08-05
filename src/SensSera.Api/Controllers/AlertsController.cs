using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public sealed class AlertsController(IAlertService alerts, IInsightService insights) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AlertResponse>>> List(
        [FromQuery] AlertQuery query, CancellationToken cancellationToken)
        => Ok(await alerts.ListAsync(query, cancellationToken));

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<AlertResponse>> Acknowledge(
        Guid id, CancellationToken cancellationToken)
        => Ok(await alerts.AcknowledgeAsync(id, cancellationToken));

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AlertResponse>> Resolve(
        Guid id, CancellationToken cancellationToken)
        => Ok(await alerts.ResolveAsync(id, cancellationToken));

    // Plain-language explanation for one alert. 404 (not 403) if it belongs to another tenant.
    // Rate-limited like /insights/ask — a cache miss triggers a paid LLM call.
    [HttpGet("{id:guid}/explain")]
    [EnableRateLimiting("insights")]
    public async Task<ActionResult<AlertExplanationDto>> Explain(
        Guid id, CancellationToken cancellationToken)
        => Ok(await insights.ExplainAlertAsync(id, cancellationToken));
}
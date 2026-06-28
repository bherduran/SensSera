using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public sealed class AlertsController(IAlertService alerts) : ControllerBase
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
}
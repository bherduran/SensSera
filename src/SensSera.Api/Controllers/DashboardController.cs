using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(CancellationToken cancellationToken)
        => Ok(await dashboard.GetSummaryAsync(cancellationToken));

    [HttpGet("greenhouses/{id:guid}")]
    public async Task<ActionResult<GreenhouseDetailResponse>> GetGreenhouse(Guid id, CancellationToken cancellationToken)
        => Ok(await dashboard.GetGreenhouseAsync(id, cancellationToken));
}

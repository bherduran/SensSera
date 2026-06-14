using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Api.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/ingest")]
[Authorize(AuthenticationSchemes = DeviceTokenAuthenticationHandler.SchemeName)]
[EnableRateLimiting("ingest")]

public sealed class IngestController(IIngestionService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ingest(IngestRequest request, CancellationToken cancellationToken)
    {
        var deviceId = Guid.Parse(User.FindFirstValue("device_id")!);
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);

        await service.IngestAsync(deviceId, orgId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch(IngestBatchRequest request, CancellationToken cancellationToken)
    {
        var deviceId = Guid.Parse(User.FindFirstValue("device_id")!);
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);

        await service.IngestBatchAsync(deviceId, orgId, request, cancellationToken);
        return NoContent();
    }
}
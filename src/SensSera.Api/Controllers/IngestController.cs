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
    public async Task<IActionResult> Ingest([FromBody] IngestRequest request, CancellationToken cancellationToken)
    {
        await service.IngestAsync(Source(), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch([FromBody] IngestBatchRequest request, CancellationToken cancellationToken)
    {
        await service.IngestBatchAsync(Source(), request, cancellationToken);
        return NoContent();
    }

    // Claims come from DeviceTokenAuthenticationHandler: org_id always, plus device_id or node_id.
    private IngestSource Source() => new(
        Guid.Parse(User.FindFirstValue("org_id")!),
        ClaimGuid("device_id"),
        ClaimGuid("node_id"));

    private Guid? ClaimGuid(string type) =>
        Guid.TryParse(User.FindFirstValue(type), out var id) ? id : null;
}
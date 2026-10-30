using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Api.Auth;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.RateLimiting;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/ingest")]
[Authorize(AuthenticationSchemes = DeviceTokenAuthenticationHandler.SchemeName)]
[EnableRateLimiting("ingest")]
public sealed partial class IngestController(IIngestionService service) : ControllerBase
{
    private const string FirmwareHeader = "X-Firmware-Version";

    [HttpPost]
    public async Task<IActionResult> Ingest(
        [FromBody] IngestRequest request,
        [FromHeader(Name = FirmwareHeader)] string? firmwareVersion,
        CancellationToken cancellationToken)
    {
        await service.IngestAsync(Source(firmwareVersion), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch(
        [FromBody] IngestBatchRequest request,
        [FromHeader(Name = FirmwareHeader)] string? firmwareVersion,
        CancellationToken cancellationToken)
    {
        await service.IngestBatchAsync(Source(firmwareVersion), request, cancellationToken);
        return NoContent();
    }

    // Claims come from DeviceTokenAuthenticationHandler: org_id always, plus device_id or node_id.
    private IngestSource Source(string? firmwareVersion) => new(
        Guid.Parse(User.FindFirstValue("org_id")!),
        ClaimGuid("device_id"),
        ClaimGuid("node_id"),
        ValidFirmwareVersion(firmwareVersion));

    // The version is only a label for the dashboard, so a malformed one is dropped rather than
    // failing the request: losing readings over it would be the worse outcome. The character set
    // covers semver with build metadata ("0.1.0+b8bd68c8", "1.2.0-rc.1") and fits Node.FirmwareVersion.
    private static string? ValidFirmwareVersion(string? value) =>
        value is not null && FirmwareVersionPattern().IsMatch(value) ? value : null;

    [GeneratedRegex(@"^[0-9A-Za-z.+_-]{1,50}\z")]
    private static partial Regex FirmwareVersionPattern();

    private Guid? ClaimGuid(string type) =>
        Guid.TryParse(User.FindFirstValue(type), out var id) ? id : null;
}
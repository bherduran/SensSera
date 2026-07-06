using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/greenhouses")]
[Authorize]
public sealed class GreenhousesController(IGreenhouseService service, IReadingService readings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GreenhouseResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}/readings")]
    public async Task<ActionResult<GreenhouseReadingsResponse>> GetReadings(
        Guid id, [FromQuery] GreenhouseReadingsQuery query, CancellationToken cancellationToken) =>
        Ok(await readings.GetGreenhouseReadingsAsync(id, query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GreenhouseResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GreenhouseResponse>> Create(GreenhouseRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GreenhouseResponse>> Update(Guid id, GreenhouseRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

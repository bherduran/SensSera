using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public sealed class DevicesController(IDeviceService service, IReadingService readings) : ControllerBase
{
    [HttpGet("greenhouse/{greenhouseId:guid}")]
    public async Task<IActionResult> GetByGreenhouse(Guid greenhouseId, CancellationToken cancellationToken) =>
        Ok(await service.GetAllByGreenhouseAsync(greenhouseId, cancellationToken));

    [HttpGet("{id:guid}/readings")]
    public async Task<IActionResult> GetReadings(
        Guid id, [FromQuery] DeviceReadingsQuery query, CancellationToken cancellationToken) =>
        Ok(await readings.GetDeviceReadingsAsync(id, query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(DeviceRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, DeviceRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/rotate-token")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RotateToken(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RotateTokenAsync(id, cancellationToken));

    
}
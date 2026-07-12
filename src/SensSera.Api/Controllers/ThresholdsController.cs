using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/thresholds")]
[Authorize]
public sealed class ThresholdsController(IThresholdService service) : ControllerBase
{
    [HttpGet("greenhouse/{greenhouseId:guid}")]
    public async Task<ActionResult<List<ThresholdResponse>>> GetByGreenhouse(Guid greenhouseId, CancellationToken cancellationToken) =>
        Ok(await service.GetAllByGreenhouseAsync(greenhouseId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ThresholdResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ThresholdResponse>> Create(ThresholdRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new {id = result.Id}, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ThresholdResponse>> Update(Guid id, ThresholdRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]   
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
    
}
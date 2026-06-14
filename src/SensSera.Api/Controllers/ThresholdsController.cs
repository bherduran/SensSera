using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/thresholds")]
public class ThresholdsController(IThresholdService service) : ControllerBase
{
    [HttpGet("greenhouse/{greenhouseId:guid}")]
    public async Task<IActionResult> GetByGreenhouse(Guid greenhouseId) =>
        Ok(await service.GetAllByGreenhouseAsync(greenhouseId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) =>
        Ok(await service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "Admin")]     
    public async Task<IActionResult> Create(ThresholdRequest request)
    {
        var result = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new {id = result.Id}, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]   
    public async Task<IActionResult> Update(Guid id, ThresholdRequest request) =>
        Ok(await service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]   
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteAsync(id);
        return NoContent();
    }
    
}
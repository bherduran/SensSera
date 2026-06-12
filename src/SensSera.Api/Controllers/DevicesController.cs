using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController(IDeviceService service) : ControllerBase
{
    [HttpGet("greenhouse/{greenhouseId:guid}")]
    public async Task<IActionResult> GetByGreenhouse(Guid greenhouseId) =>
        Ok(await service.GetAllByGreenhouseAsync(greenhouseId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) =>
        Ok(await service.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create(DeviceRequest request)
    {
        var result = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id}, result);
    }            

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DeviceRequest request) =>
        Ok(await service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteAsync(id);
        return NoContent();
    }    

    [HttpPost("{id:guid}/rotate-token")]
    public async Task<IActionResult> RotateToken(Guid id) =>
        Ok(await service.RotateTokenAsync(id));

    
}
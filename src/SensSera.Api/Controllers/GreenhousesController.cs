using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/greenhouses")]
public class GreenhousesController(IGreenhouseService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await service.GetAllAsync());


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) =>
        Ok(await service.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create(GreenhouseRequest request)
    {
        var result = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id}, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, GreenhouseRequest request) => 
    Ok(await service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteAsync(id);
        return NoContent(); 
    }    
}
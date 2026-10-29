using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/nodes")]
[Authorize]
public sealed class NodesController(INodeService service) : ControllerBase
{
    [HttpGet("greenhouse/{greenhouseId:guid}")]
    public async Task<ActionResult<List<NodeResponse>>> GetByGreenhouse(Guid greenhouseId, CancellationToken cancellationToken) =>
        Ok(await service.GetAllByGreenhouseAsync(greenhouseId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NodeResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NodeWithTokenResponse>> Create(NodeRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // Removes the node with all its channel devices and their history. A single channel is
    // removed with DELETE /api/devices/{id}.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/rotate-token")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NodeWithTokenResponse>> RotateToken(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RotateTokenAsync(id, cancellationToken));

    [HttpPost("{id:guid}/channels")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NodeChannelResponse>> AddChannel(Guid id, NodeChannelRequest request, CancellationToken cancellationToken)
    {
        var result = await service.AddChannelAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(DevicesController.GetById), "Devices", new { id = result.DeviceId }, result);
    }
}

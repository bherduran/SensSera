using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    // Liveness + DB reachability, so a container orchestrator doesn't route to an API
    // that is up but can't reach Postgres.
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var database = await db.Database.CanConnectAsync(cancellationToken);
        var body = new { status = database ? "healthy" : "unhealthy", database = database ? "up" : "down" };
        return database ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}

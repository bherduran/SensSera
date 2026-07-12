using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SensSera.Api.Hubs;

/// <summary>
/// Live telemetry hub at /hubs/telemetry. On connect, each client joins the
/// group for its own organization — derived from the trusted org_id claim,
/// never from client input — so a tenant can only receive its own events.
/// </summary>
[Authorize]
public sealed class TelemetryHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var orgId = Context.User?.FindFirst("org_id")?.Value;
        if (Guid.TryParse(orgId, out var organizationId))
            await Groups.AddToGroupAsync(Context.ConnectionId, OrgGroup(organizationId));

        await base.OnConnectedAsync();
    }

    public static string OrgGroup(Guid organizationId) => $"org-{organizationId}";
}

using Microsoft.AspNetCore.SignalR;
using SensSera.Api.Hubs;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;

namespace SensSera.Api.Realtime;

/// <summary>
/// SignalR-backed <see cref="IRealtimeNotifier"/>. Fans events out only to the
/// caller's tenant group. Depends solely on the singleton IHubContext, so it is
/// safe to inject into scoped services and singleton background jobs alike.
/// </summary>
public sealed class SignalRNotifier(IHubContext<TelemetryHub> hub) : IRealtimeNotifier
{
    public Task ReadingReceivedAsync(Guid organizationId, ReadingReceivedEvent payload, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(TelemetryHub.OrgGroup(organizationId))
            .SendAsync("ReadingReceived", payload, cancellationToken);

    public Task AlertRaisedAsync(Guid organizationId, AlertRaisedEvent payload, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(TelemetryHub.OrgGroup(organizationId))
            .SendAsync("AlertRaised", payload, cancellationToken);
}

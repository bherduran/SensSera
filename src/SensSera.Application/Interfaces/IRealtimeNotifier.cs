using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

/// <summary>
/// Abstraction over the real-time transport (SignalR). Callers know the
/// tenant (organizationId) but never touch the hub directly, so Infrastructure
/// can publish without referencing the Api layer.
/// </summary>
public interface IRealtimeNotifier
{
    Task ReadingReceivedAsync(Guid organizationId, ReadingReceivedEvent payload, CancellationToken cancellationToken = default);

    Task AlertRaisedAsync(Guid organizationId, AlertRaisedEvent payload, CancellationToken cancellationToken = default);
}

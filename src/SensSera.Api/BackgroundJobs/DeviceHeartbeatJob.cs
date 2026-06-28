using Microsoft.EntityFrameworkCore;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Api.BackgroundJobs;

public sealed class DeviceHeartbeatJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DeviceHeartbeatJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan OfflineThreshold = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "DeviceHeartbeatJob cycle failed"); }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now - OfflineThreshold;

        await db.Devices
            .IgnoreQueryFilters()
            .Where(d => d.Status == DeviceStatus.Active
                     && d.LastSeenAt != null
                     && d.LastSeenAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DeviceStatus.Inactive)
                .SetProperty(d => d.UpdatedAt, now),
                cancellationToken);
    }
}

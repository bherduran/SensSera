using Microsoft.EntityFrameworkCore;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Api.BackgroundJobs;

public sealed class RollupJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RollupJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RollupOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "RollupJob cycle failed"); }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RollupOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var hourEnd = hourStart.AddHours(1);

        var devices = await db.Devices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(d => new { d.Id, d.OrganizationId, d.Metric })
            .ToListAsync(cancellationToken);

        foreach (var d in devices)
        {
            var stats = await db.SensorReadings
                .IgnoreQueryFilters()
                .Where(r => r.DeviceId == d.Id && r.RecordedAt >= hourStart && r.RecordedAt < hourEnd)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Min = g.Min(r => r.Value),
                    Max = g.Max(r => r.Value),
                    Avg = g.Average(r => r.Value),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (stats is null) continue;

            var existing = await db.ReadingRollups
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.OrganizationId == d.OrganizationId
                                       && r.DeviceId == d.Id
                                       && r.Metric == d.Metric
                                       && r.Bucket == RollupBucket.Hour
                                       && r.PeriodStart == hourStart, cancellationToken);

            if (existing is null)
            {
                db.ReadingRollups.Add(new ReadingRollup
                {
                    OrganizationId = d.OrganizationId,
                    DeviceId = d.Id,
                    Metric = d.Metric,
                    Bucket = RollupBucket.Hour,
                    PeriodStart = hourStart,
                    Min = stats.Min,
                    Max = stats.Max,
                    Avg = stats.Avg,
                    Count = stats.Count,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            else
            {
                existing.Min = stats.Min;
                existing.Max = stats.Max;
                existing.Avg = stats.Avg;
                existing.Count = stats.Count;
                existing.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

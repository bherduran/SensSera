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

    private sealed record Stats(Guid OrganizationId, Guid DeviceId, MetricType Metric, double Min, double Max, double Avg, int Count);

    private async Task RollupOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

        // The previous hour is re-rolled too, so readings that arrive late (or a cycle that ran just
        // before the hour turned) still land in the closed bucket.
        foreach (var hourStart in new[] { currentHour.AddHours(-1), currentHour })
        {
            var hourEnd = hourStart.AddHours(1);
            var stats = await db.SensorReadings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.RecordedAt >= hourStart && r.RecordedAt < hourEnd)
                .GroupBy(r => new { r.OrganizationId, r.DeviceId, r.Metric })
                .Select(g => new Stats(g.Key.OrganizationId, g.Key.DeviceId, g.Key.Metric,
                    g.Min(r => r.Value), g.Max(r => r.Value), g.Average(r => r.Value), g.Count()))
                .ToListAsync(cancellationToken);

            await UpsertAsync(db, RollupBucket.Hour, hourStart, stats, now, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);

        // Day buckets are derived from the hour rollups (count-weighted), not from a rescan of raw
        // readings. Yesterday is refreshed as well so its last hour is included after midnight.
        foreach (var dayStart in new[] { today.AddDays(-1), today })
        {
            var dayEnd = dayStart.AddDays(1);
            var stats = await db.ReadingRollups
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Bucket == RollupBucket.Hour && r.PeriodStart >= dayStart && r.PeriodStart < dayEnd)
                .GroupBy(r => new { r.OrganizationId, r.DeviceId, r.Metric })
                .Select(g => new Stats(g.Key.OrganizationId, g.Key.DeviceId, g.Key.Metric,
                    g.Min(r => r.Min), g.Max(r => r.Max),
                    g.Sum(r => r.Avg * r.Count) / g.Sum(r => r.Count), g.Sum(r => r.Count)))
                .ToListAsync(cancellationToken);

            await UpsertAsync(db, RollupBucket.Day, dayStart, stats, now, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    // One query loads every existing rollup for the period; new ones are added, the rest updated.
    private static async Task UpsertAsync(
        AppDbContext db, RollupBucket bucket, DateTime periodStart, List<Stats> stats, DateTime now,
        CancellationToken cancellationToken)
    {
        if (stats.Count == 0) return;

        var existing = await db.ReadingRollups
            .IgnoreQueryFilters()
            .Where(r => r.Bucket == bucket && r.PeriodStart == periodStart)
            .ToDictionaryAsync(r => (r.DeviceId, r.Metric), cancellationToken);

        foreach (var s in stats)
        {
            if (existing.TryGetValue((s.DeviceId, s.Metric), out var rollup))
            {
                rollup.Min = s.Min;
                rollup.Max = s.Max;
                rollup.Avg = s.Avg;
                rollup.Count = s.Count;
                rollup.UpdatedAt = now;
                continue;
            }

            db.ReadingRollups.Add(new ReadingRollup
            {
                OrganizationId = s.OrganizationId,
                DeviceId = s.DeviceId,
                Metric = s.Metric,
                Bucket = bucket,
                PeriodStart = periodStart,
                Min = s.Min,
                Max = s.Max,
                Avg = s.Avg,
                Count = s.Count,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }
}

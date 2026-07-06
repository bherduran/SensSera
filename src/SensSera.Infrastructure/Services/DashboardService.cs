using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class DashboardService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IDashboardService
{
    // How far back a rollup bucket can be and still count as "current" (RollupJob upserts the current hour every 5 min).
    private static readonly TimeSpan CurrentWindow = TimeSpan.FromHours(3);
    private static readonly TimeSpan DetailWindow = TimeSpan.FromHours(24);

    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        _ = tenant.RequireOrganizationId();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var greenhouses = await db.Greenhouses
            .AsNoTracking()
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        var devices = await db.Devices
            .AsNoTracking()
            .Select(d => new { d.Id, d.GreenhouseId })
            .ToListAsync(cancellationToken);

        // Active alert count per greenhouse — one grouped query.
        var alertCounts = (await db.Alerts
            .AsNoTracking()
            .Where(a => a.Status != AlertStatus.Resolved)
            .GroupBy(a => a.GreenhouseId)
            .Select(g => new { GreenhouseId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.GreenhouseId, x => x.Count);

        // Latest few hourly buckets per device → "current" value per metric. Bounded read (a handful of buckets per device).
        var currentFrom = now - CurrentWindow;
        var recent = await db.ReadingRollups
            .AsNoTracking()
            .Where(r => r.Bucket == RollupBucket.Hour && r.PeriodStart >= currentFrom)
            .Select(r => new { r.DeviceId, r.Metric, r.PeriodStart, r.Avg })
            .ToListAsync(cancellationToken);

        var deviceToGreenhouse = devices.ToDictionary(d => d.Id, d => d.GreenhouseId);

        // (greenhouseId, metric) → latest bucket avg
        var currentByGreenhouse = recent
            .Where(r => deviceToGreenhouse.ContainsKey(r.DeviceId))
            .GroupBy(r => (Greenhouse: deviceToGreenhouse[r.DeviceId], r.Metric))
            .Select(g => new
            {
                g.Key.Greenhouse,
                g.Key.Metric,
                Current = g.OrderByDescending(x => x.PeriodStart).First().Avg
            })
            .ToLookup(x => x.Greenhouse);

        var deviceCounts = devices
            .GroupBy(d => d.GreenhouseId)
            .ToDictionary(g => g.Key, g => g.Count());

        var summaries = greenhouses
            .Select(g => new GreenhouseSummary(
                g.Id,
                g.Name,
                deviceCounts.GetValueOrDefault(g.Id, 0),
                alertCounts.GetValueOrDefault(g.Id, 0),
                currentByGreenhouse[g.Id]
                    .OrderBy(m => m.Metric)
                    .Select(m => new MetricCurrent(m.Metric.ToString(), Math.Round(m.Current, 2)))
                    .ToList()))
            .ToList();

        return new DashboardSummaryResponse(summaries);
    }

    public async Task<GreenhouseDetailResponse> GetGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default)
    {
        _ = tenant.RequireOrganizationId();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var greenhouse = await db.Greenhouses
            .AsNoTracking()
            .Where(g => g.Id == greenhouseId)
            .Select(g => new { g.Id, g.Name, g.Location })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Greenhouse {greenhouseId} not found");

        var deviceIds = await db.Devices
            .AsNoTracking()
            .Where(d => d.GreenhouseId == greenhouseId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var windowStart = now - DetailWindow;
        var rollups = await db.ReadingRollups
            .AsNoTracking()
            .Where(r => deviceIds.Contains(r.DeviceId)
                     && r.Bucket == RollupBucket.Hour
                     && r.PeriodStart >= windowStart)
            .Select(r => new { r.Metric, r.PeriodStart, r.Min, r.Max, r.Avg, r.Count })
            .ToListAsync(cancellationToken);

        var metrics = rollups
            .GroupBy(r => r.Metric)
            .Select(g =>
            {
                var totalCount = g.Sum(x => x.Count);
                var weightedAvg = totalCount > 0 ? g.Sum(x => x.Avg * x.Count) / totalCount : 0;
                var current = g.OrderByDescending(x => x.PeriodStart).First().Avg;
                return new MetricSummary(
                    g.Key.ToString(),
                    Math.Round(current, 2),
                    Math.Round(g.Min(x => x.Min), 2),
                    Math.Round(g.Max(x => x.Max), 2),
                    Math.Round(weightedAvg, 2));
            })
            .OrderBy(m => m.Metric)
            .ToList();

        var activeAlerts = await db.Alerts
            .AsNoTracking()
            .Where(a => a.GreenhouseId == greenhouseId && a.Status != AlertStatus.Resolved)
            .OrderByDescending(a => a.TriggeredAt)
            .Select(a => new AlertResponse(
                a.Id, a.GreenhouseId, a.DeviceId, a.ThresholdId,
                a.Metric.ToString(), a.TriggeredValue, a.Severity.ToString(), a.Status.ToString(),
                a.TriggeredAt, a.ResolvedAt))
            .ToListAsync(cancellationToken);

        return new GreenhouseDetailResponse(greenhouse.Id, greenhouse.Name, greenhouse.Location, metrics, activeAlerts);
    }
}

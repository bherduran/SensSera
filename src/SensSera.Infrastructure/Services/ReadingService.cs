using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class ReadingService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IReadingService
{
    private const int MaxReadings = 1000;
    private const int MaxPoints = 1000;

    public async Task<DeviceReadingsResponse> GetDeviceReadingsAsync(
        Guid deviceId, DeviceReadingsQuery query, CancellationToken cancellationToken = default)
    {
        _ = tenant.RequireOrganizationId();

        // Global filter scopes to the tenant; a cross-tenant/unknown id resolves to null → 404.
        var device = await db.Devices
            .AsNoTracking()
            .Where(d => d.Id == deviceId)
            .Select(d => new { d.Id, d.Metric })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Device {deviceId} not found");

        var limit = Math.Clamp(query.Limit, 1, MaxReadings);

        var q = db.SensorReadings
            .AsNoTracking()
            .Where(r => r.DeviceId == deviceId);

        if (query.From is { } from)
            q = q.Where(r => r.RecordedAt >= from);
        if (query.To is { } to)
            q = q.Where(r => r.RecordedAt <= to);

        var readings = await q
            .OrderByDescending(r => r.RecordedAt)
            .Take(limit)
            .Select(r => new ReadingPoint(r.RecordedAt, r.Value))
            .ToListAsync(cancellationToken);

        return new DeviceReadingsResponse(device.Id, device.Metric.ToString(), readings);
    }

    public async Task<GreenhouseReadingsResponse> GetGreenhouseReadingsAsync(
        Guid greenhouseId, GreenhouseReadingsQuery query, CancellationToken cancellationToken = default)
    {
        _ = tenant.RequireOrganizationId();

        var greenhouseExists = await db.Greenhouses
            .AsNoTracking()
            .AnyAsync(g => g.Id == greenhouseId, cancellationToken);
        if (!greenhouseExists)
            throw new KeyNotFoundException($"Greenhouse {greenhouseId} not found");

        // A rollup series is per-metric — a mixed-metric chart is meaningless, so metric is required here.
        if (string.IsNullOrWhiteSpace(query.Metric) ||
            !Enum.TryParse<MetricType>(query.Metric, ignoreCase: true, out var metric))
            throw new ArgumentException("A valid 'metric' query parameter is required.");

        var bucket = Enum.TryParse<RollupBucket>(query.Bucket, ignoreCase: true, out var b) ? b : RollupBucket.Hour;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var to = query.To ?? now;
        var from = query.From ?? (bucket == RollupBucket.Day ? to.AddDays(-30) : to.AddHours(-24));

        var deviceIds = await db.Devices
            .AsNoTracking()
            .Where(d => d.GreenhouseId == greenhouseId && d.Metric == metric)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var rows = await db.ReadingRollups
            .AsNoTracking()
            .Where(r => deviceIds.Contains(r.DeviceId)
                     && r.Bucket == bucket
                     && r.PeriodStart >= from
                     && r.PeriodStart <= to)
            .Select(r => new { r.PeriodStart, r.Min, r.Max, r.Avg, r.Count })
            .ToListAsync(cancellationToken);

        // Combine the greenhouse's devices of this metric per bucket into one series point.
        var points = rows
            .GroupBy(r => r.PeriodStart)
            .OrderByDescending(g => g.Key)
            .Take(MaxPoints)
            .Select(g =>
            {
                var totalCount = g.Sum(x => x.Count);
                var weightedAvg = totalCount > 0 ? g.Sum(x => x.Avg * x.Count) / totalCount : 0;
                return new RollupPoint(
                    g.Key,
                    Math.Round(g.Min(x => x.Min), 2),
                    Math.Round(g.Max(x => x.Max), 2),
                    Math.Round(weightedAvg, 2),
                    totalCount);
            })
            .OrderBy(p => p.PeriodStart)
            .ToList();

        return new GreenhouseReadingsResponse(greenhouseId, metric.ToString(), bucket.ToString(), points);
    }
}

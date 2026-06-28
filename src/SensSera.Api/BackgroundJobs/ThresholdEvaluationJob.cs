using Microsoft.EntityFrameworkCore;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Api.BackgroundJobs;

public sealed class ThresholdEvaluationJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ThresholdEvaluationJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await EvaluateOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "ThresholdEvaluationJob cycle failed"); }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }    
    }

    private async Task EvaluateOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var thresholds = await db.Thresholds
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var t in thresholds)
        {
            var devices = await db.Devices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.GreenhouseId == t.GreenhouseId && d.Metric == t.Metric)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            if (devices.Count == 0) continue;

            var latest = await db.SensorReadings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => devices.Contains(r.DeviceId))
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefaultAsync(cancellationToken);    

            if (latest is null) continue;

            var outOfRange = 
                (t.MinValue is { } min && latest.Value < min)  ||
                (t.MaxValue is { } max && latest.Value > max);    

            if (!outOfRange) continue;

            var hasOpen = await db.Alerts
                .IgnoreQueryFilters()
                .AnyAsync(a => a.ThresholdId == t.Id && a.Status == AlertStatus.Open, cancellationToken);

            if (hasOpen) continue;

            db.Alerts.Add(new Alert
            {
                OrganizationId = t.OrganizationId,
                GreenhouseId = t.GreenhouseId,
                DeviceId = latest.DeviceId,
                ThresholdId = t.Id,
                Metric = t.Metric,
                TriggeredValue = latest.Value,
                Severity = AlertSeverity.Warning,
                Status = AlertStatus.Open,
                TriggeredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }    

        await db.SaveChangesAsync(cancellationToken);
    }
}
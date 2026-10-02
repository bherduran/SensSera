using Microsoft.EntityFrameworkCore;
using SensSera.Application.Alerting;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Api.BackgroundJobs;

public sealed class ThresholdEvaluationJob(
    IServiceScopeFactory scopeFactory,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<ThresholdEvaluationJob> logger) : BackgroundService
{
    // 30s by default; integration tests shorten it to observe an alert without a long wait.
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(
        configuration.GetValue("Jobs:ThresholdEvaluationIntervalSeconds", 30));

    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
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

        if (thresholds.Count == 0) return;

        // Everything the loop needs is loaded up front: a fixed number of queries per cycle,
        // however many thresholds and devices there are.
        var greenhouseIds = thresholds.Select(t => t.GreenhouseId).Distinct().ToList();
        var devices = await db.Devices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => greenhouseIds.Contains(d.GreenhouseId))
            .Select(d => new { d.Id, d.GreenhouseId, d.Metric })
            .ToListAsync(cancellationToken);

        // Readings older than a day are stale; a silent device is the heartbeat job's concern, not a
        // fresh breach. The window also keeps this on the (DeviceId, RecordedAt) index.
        var deviceIds = devices.Select(d => d.Id).ToList();
        var since = now - StaleAfter;
        var latestByDevice = await db.SensorReadings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => deviceIds.Contains(r.DeviceId) && r.RecordedAt >= since)
            .GroupBy(r => r.DeviceId)
            .Select(g => g.OrderByDescending(r => r.RecordedAt).First())
            .ToDictionaryAsync(r => r.DeviceId, cancellationToken);

        var thresholdIds = thresholds.Select(t => t.Id).ToList();
        var withOpenAlert = (await db.Alerts
            .IgnoreQueryFilters()
            .Where(a => thresholdIds.Contains(a.ThresholdId) && a.Status == AlertStatus.Open)
            .Select(a => a.ThresholdId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var raised = new List<Alert>();

        foreach (var t in thresholds)
        {
            if (withOpenAlert.Contains(t.Id)) continue;

            var latest = devices
                .Where(d => d.GreenhouseId == t.GreenhouseId && d.Metric == t.Metric)
                .Select(d => latestByDevice.GetValueOrDefault(d.Id))
                .OfType<SensorReading>()
                .MaxBy(r => r.RecordedAt);

            if (latest is null) continue;

            var outOfRange =
                (t.MinValue is { } min && latest.Value < min) ||
                (t.MaxValue is { } max && latest.Value > max);

            if (!outOfRange) continue;

            var alert = new Alert
            {
                OrganizationId = t.OrganizationId,
                GreenhouseId = t.GreenhouseId,
                DeviceId = latest.DeviceId,
                ThresholdId = t.Id,
                Metric = t.Metric,
                TriggeredValue = latest.Value,
                Severity = AlertSeverityRule.For(latest.Value, t.MinValue, t.MaxValue),
                Status = AlertStatus.Open,
                TriggeredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Alerts.Add(alert);
            raised.Add(alert);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Ids are populated after save; push each new alert to its tenant.
        foreach (var alert in raised)
            await PushAlertAsync(alert, cancellationToken);
    }

    // An alert is already persisted; a real-time push failure must not stop the cycle.
    private async Task PushAlertAsync(Alert alert, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.AlertRaisedAsync(
                alert.OrganizationId,
                new AlertRaisedEvent(alert.Id, alert.GreenhouseId, alert.Metric, alert.Severity, alert.TriggeredAt),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Real-time push failed for alert {AlertId}", alert.Id);
        }
    }
}
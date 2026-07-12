using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class IngestionService(
    AppDbContext db,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider,
    ILogger<IngestionService> logger) : IIngestionService
{
    public async Task IngestAsync(Guid deviceId, Guid organizationId, IngestRequest request, CancellationToken cancellationToken = default)
    {
        if(!Enum.TryParse<MetricType>(request.Metric, ignoreCase: true, out var metric))
            throw new ArgumentException($"Unknown metric: {request.Metric}");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reading = new SensorReading
        {
            DeviceId = deviceId,
            OrganizationId = organizationId,
            Metric = metric,
            Value = request.Value,
            RecordedAt = request.RecordedAt,
            IngestedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.SensorReadings.Add(reading);

        await db.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastSeenAt, timeProvider.GetUtcNow().UtcDateTime)
                .SetProperty(d => d.Status, DeviceStatus.Active), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var greenhouseId = await db.Devices
            .AsNoTracking()
            .Where(d => d.Id == deviceId)
            .Select(d => d.GreenhouseId)
            .FirstOrDefaultAsync(cancellationToken);

        await PushReadingAsync(organizationId, greenhouseId, reading, metric, cancellationToken);
    }

    public async Task IngestBatchAsync(Guid deviceId, Guid organizationId, IngestBatchRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var r in request.Readings)
            await IngestAsync(deviceId, organizationId, r, cancellationToken);
    }

    // The reading is already persisted; a real-time push failure must not fail ingestion.
    private async Task PushReadingAsync(
        Guid organizationId, Guid greenhouseId, SensorReading reading, MetricType metric, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.ReadingReceivedAsync(
                organizationId,
                new ReadingReceivedEvent(greenhouseId, reading.DeviceId, metric, reading.Value, reading.RecordedAt),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Real-time push failed for reading on device {DeviceId}", reading.DeviceId);
        }
    }
}

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
    public Task IngestAsync(Guid deviceId, Guid organizationId, IngestRequest request, CancellationToken cancellationToken = default) =>
        IngestManyAsync(deviceId, organizationId, [request], cancellationToken);

    public Task IngestBatchAsync(Guid deviceId, Guid organizationId, IngestBatchRequest request, CancellationToken cancellationToken = default) =>
        IngestManyAsync(deviceId, organizationId, request.Readings, cancellationToken);

    // One INSERT batch + one LastSeenAt update per request, however many readings it carries.
    private async Task IngestManyAsync(
        Guid deviceId, Guid organizationId, IReadOnlyList<IngestRequest> requests, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var readings = requests.Select(r => new SensorReading
        {
            DeviceId = deviceId,
            OrganizationId = organizationId,
            Metric = ParseMetric(r.Metric),
            Value = r.Value,
            RecordedAt = ToUtc(r.RecordedAt),
            IngestedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

        db.SensorReadings.AddRange(readings);

        await db.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastSeenAt, now)
                .SetProperty(d => d.Status, DeviceStatus.Active), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var greenhouseId = await db.Devices
            .AsNoTracking()
            .Where(d => d.Id == deviceId)
            .Select(d => d.GreenhouseId)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var reading in readings)
            await PushReadingAsync(organizationId, greenhouseId, reading, cancellationToken);
    }

    private static MetricType ParseMetric(string metric) =>
        Enum.TryParse<MetricType>(metric, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown metric: {metric}");

    // System.Text.Json yields Kind=Local for offsets like "+03:00" and Kind=Unspecified when no
    // offset is given; Npgsql only accepts UTC for timestamptz. Devices are expected to send UTC,
    // so an offset-less value is taken as UTC rather than the server's local zone.
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    // The reading is already persisted; a real-time push failure must not fail ingestion.
    private async Task PushReadingAsync(
        Guid organizationId, Guid greenhouseId, SensorReading reading, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.ReadingReceivedAsync(
                organizationId,
                new ReadingReceivedEvent(greenhouseId, reading.DeviceId, reading.Metric, reading.Value, reading.RecordedAt),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Real-time push failed for reading on device {DeviceId}", reading.DeviceId);
        }
    }
}

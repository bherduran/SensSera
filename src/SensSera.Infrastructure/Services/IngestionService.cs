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
    public Task IngestAsync(IngestSource source, IngestRequest request, CancellationToken cancellationToken = default) =>
        IngestManyAsync(source, [request], cancellationToken);

    public Task IngestBatchAsync(IngestSource source, IngestBatchRequest request, CancellationToken cancellationToken = default) =>
        IngestManyAsync(source, request.Readings, cancellationToken);

    // One INSERT batch + one LastSeenAt update per request, however many readings it carries.
    private async Task IngestManyAsync(
        IngestSource source, IReadOnlyList<IngestRequest> requests, CancellationToken cancellationToken)
    {
        // Resolve and check every reading before writing anything: a batch is all-or-nothing.
        var targets = await ResolveTargetsAsync(source, requests, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var readings = requests.Select((r, i) => new SensorReading
        {
            DeviceId = targets[i].Id,
            OrganizationId = source.OrganizationId,
            Metric = targets[i].Metric,
            Value = r.Value,
            RecordedAt = ToUtc(r.RecordedAt),
            IngestedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

        db.SensorReadings.AddRange(readings);

        var deviceIds = targets.Select(t => t.Id).Distinct().ToList();
        await db.Devices
            .Where(d => deviceIds.Contains(d.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastSeenAt, now)
                .SetProperty(d => d.Status, DeviceStatus.Active), cancellationToken);

        if (source.NodeId is { } nodeId)
            await db.Nodes
                .Where(n => n.Id == nodeId)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.LastSeenAt, now), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < readings.Count; i++)
            await PushReadingAsync(source.OrganizationId, targets[i].GreenhouseId, readings[i], cancellationToken);
    }

    private sealed record Target(Guid Id, MetricType Metric, string? Channel, Guid GreenhouseId);

    // Maps each reading to the device it belongs to: the token's own device, or for a node the
    // device registered on the reading's channel. The reading's metric must match that device's
    // metric, so a miswired channel is rejected instead of silently stored under the wrong sensor.
    private async Task<IReadOnlyList<Target>> ResolveTargetsAsync(
        IngestSource source, IReadOnlyList<IngestRequest> requests, CancellationToken cancellationToken)
    {
        var query = db.Devices.AsNoTracking();
        query = source.NodeId is { } nodeId
            ? query.Where(d => d.NodeId == nodeId)
            : query.Where(d => d.Id == source.DeviceId);
        var devices = await query
            .Select(d => new Target(d.Id, d.Metric, d.Channel, d.GreenhouseId))
            .ToListAsync(cancellationToken);

        var byChannel = devices.Where(d => d.Channel is not null).ToDictionary(d => d.Channel!, StringComparer.Ordinal);

        return requests.Select((r, i) =>
        {
            var prefix = requests.Count > 1 ? $"Reading {i}: " : "";
            Target target;
            if (source.NodeId is null)
            {
                if (r.Channel is not null)
                    throw new ArgumentException($"{prefix}channel is only valid with a node token.");
                // Deleted between authentication and here: treat like a revoked token.
                target = devices.SingleOrDefault()
                    ?? throw new UnauthorizedAccessException("Device no longer exists.");
            }
            else
            {
                if (r.Channel is null)
                    throw new ArgumentException($"{prefix}channel is required with a node token.");
                if (!byChannel.TryGetValue(r.Channel, out target!))
                    throw new ArgumentException($"{prefix}unknown channel '{r.Channel}'.");
            }

            var metric = ParseMetric(r.Metric);
            if (metric != target.Metric)
                throw new ArgumentException(target.Channel is null
                    ? $"{prefix}this device measures {target.Metric}, not {metric}."
                    : $"{prefix}channel '{target.Channel}' measures {target.Metric}, not {metric}.");
            return target;
        }).ToList();
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

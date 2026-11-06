using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SensSera.Application.DTOs;
using SensSera.Application.Ingestion;
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
    // Matches the column length; a 100-reading batch can produce more text than is useful anyway.
    private const int MaxRejectReasonLength = 300;

    public async Task IngestAsync(IngestSource source, IngestRequest request, CancellationToken cancellationToken = default)
    {
        var result = await IngestManyAsync(source, [request], cancellationToken);
        if (result.Rejected is [var rejected])
            throw new ArgumentException(Capitalize(rejected.Reason));
    }

    public Task<IngestBatchResponse> IngestBatchAsync(IngestSource source, IngestBatchRequest request, CancellationToken cancellationToken = default) =>
        IngestManyAsync(source, request.Readings, cancellationToken);

    // One INSERT batch + one LastSeenAt update per request, however many readings it carries.
    private async Task<IngestBatchResponse> IngestManyAsync(
        IngestSource source, IReadOnlyList<IngestRequest> requests, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var devices = await LoadTargetsAsync(source, cancellationToken);
        // Deleted between authentication and here: treat like a revoked token.
        if (source.NodeId is null && devices.Count == 0)
            throw new UnauthorizedAccessException("Device no longer exists.");

        var byChannel = devices.Where(d => d.Channel is not null).ToDictionary(d => d.Channel!, StringComparer.Ordinal);
        var accepted = new List<(SensorReading Reading, Target Target)>();
        var rejected = new List<RejectedReading>();

        for (var i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            Target? target = null;
            var reason = ReadingRules.Check(r, now) ?? Match(source, r, devices, byChannel, out target);
            if (reason is not null)
            {
                rejected.Add(new RejectedReading(i, reason));
                continue;
            }

            var device = target!; // Match sets it whenever it returns no reason
            accepted.Add((new SensorReading
            {
                DeviceId = device.Id,
                OrganizationId = source.OrganizationId,
                Metric = device.Metric,
                Value = r.Value,
                RecordedAt = ReadingRules.ToUtc(r.RecordedAt),
                IngestedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            }, device));
        }

        db.SensorReadings.AddRange(accepted.Select(a => a.Reading));

        var deviceIds = accepted.Select(a => a.Target.Id).Distinct().ToList();
        if (deviceIds.Count > 0)
            await db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.LastSeenAt, now)
                    .SetProperty(d => d.Status, DeviceStatus.Active), cancellationToken);

        // The node is alive even when every reading was rejected. COALESCE keeps the stored version
        // when a request doesn't report one (older firmware).
        if (source.NodeId is { } nodeId)
        {
            var nodes = db.Nodes.Where(n => n.Id == nodeId);
            if (rejected.Count == 0)
                await nodes.ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.LastSeenAt, now)
                    .SetProperty(n => n.FirmwareVersion, n => source.FirmwareVersion ?? n.FirmwareVersion), cancellationToken);
            else
            {
                // Shown on the node card: with a 2xx the board keeps going, so this is where a
                // channel that was deleted in the web app (or a sensor gone haywire) becomes visible.
                var summary = Summarize(rejected, requests.Count);
                await nodes.ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.LastSeenAt, now)
                    .SetProperty(n => n.FirmwareVersion, n => source.FirmwareVersion ?? n.FirmwareVersion)
                    .SetProperty(n => n.LastRejectedAt, now)
                    .SetProperty(n => n.LastRejectReason, summary), cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (rejected.Count > 0)
            logger.LogWarning("Ingest rejected {Rejected} of {Total} readings from {Source}: {FirstReason}",
                rejected.Count, requests.Count, source.NodeId ?? source.DeviceId, rejected[0].Reason);

        foreach (var (reading, target) in accepted)
            await PushReadingAsync(source.OrganizationId, target.GreenhouseId, reading, cancellationToken);

        return new IngestBatchResponse(accepted.Count, rejected);
    }

    private sealed record Target(Guid Id, MetricType Metric, string? Channel, Guid GreenhouseId);

    private async Task<List<Target>> LoadTargetsAsync(IngestSource source, CancellationToken cancellationToken)
    {
        var query = db.Devices.AsNoTracking();
        query = source.NodeId is { } nodeId
            ? query.Where(d => d.NodeId == nodeId)
            : query.Where(d => d.Id == source.DeviceId);
        return await query
            .Select(d => new Target(d.Id, d.Metric, d.Channel, d.GreenhouseId))
            .ToListAsync(cancellationToken);
    }

    // Maps a reading to the device it belongs to: the token's own device, or for a node the device
    // registered on the reading's channel. The reading's metric must match that device's metric, so
    // a miswired channel is rejected instead of silently stored under the wrong sensor.
    private static string? Match(
        IngestSource source, IngestRequest r, List<Target> devices, Dictionary<string, Target> byChannel,
        out Target? target)
    {
        target = null;
        if (source.NodeId is null)
        {
            if (r.Channel is not null) return "channel is only valid with a node token.";
            target = devices[0];
        }
        else
        {
            if (r.Channel is null) return "channel is required with a node token.";
            if (!byChannel.TryGetValue(r.Channel, out target))
                return $"unknown channel '{ReadingRules.Clip(r.Channel)}'; register it for this board in the web app.";
        }

        var metric = ReadingRules.MetricOf(r);
        if (metric == target.Metric) return null;
        var reason = target.Channel is null
            ? $"this device measures {target.Metric}, not {metric}."
            : $"channel '{target.Channel}' measures {target.Metric}, not {metric}.";
        target = null;
        return reason;
    }

    private static string Summarize(List<RejectedReading> rejected, int total)
    {
        var first = rejected[0];
        var text = $"{rejected.Count} of {total} readings rejected. Reading {first.Index}: {first.Reason}";
        return text.Length <= MaxRejectReasonLength ? text : text[..(MaxRejectReasonLength - 1)] + "…";
    }

    private static string Capitalize(string reason) => char.ToUpperInvariant(reason[0]) + reason[1..];

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

using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;                                                                                                                       
using SensSera.Application.Interfaces;                                                            
using SensSera.Domain.Entities;                        
using SensSera.Domain.Enums;                   
using SensSera.Infrastructure.Persistence;  

namespace SensSera.Infrastructure.Services;

public sealed class IngestionService(AppDbContext db, TimeProvider timeProvider) : IIngestionService
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
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeenAt, timeProvider.GetUtcNow().UtcDateTime), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task IngestBatchAsync(Guid deviceId, Guid organizationId, IngestBatchRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var r in request.Readings)
            await IngestAsync(deviceId, organizationId, r, cancellationToken);
    }
}
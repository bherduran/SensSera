using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IIngestionService
{
    Task IngestAsync(Guid deviceId, Guid organizationId, IngestRequest request, CancellationToken cancellationToken = default);
    Task IngestBatchAsync(Guid deviceId, Guid organizationId, IngestBatchRequest request, CancellationToken cancellationToken = default);
}
using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IIngestionService
{
    Task IngestAsync(Guid deviceId, Guid organizationId, IngestRequest request);
    Task IngestBatchAsync(Guid deviceId, Guid organizationId, IngestBatchRequest request);
}
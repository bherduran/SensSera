using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IIngestionService
{
    Task IngestAsync(IngestSource source, IngestRequest request, CancellationToken cancellationToken = default);
    Task IngestBatchAsync(IngestSource source, IngestBatchRequest request, CancellationToken cancellationToken = default);
}

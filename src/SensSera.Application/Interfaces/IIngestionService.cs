using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IIngestionService
{
    /// <summary>Throws <see cref="ArgumentException"/> (400) when the reading is rejected.</summary>
    Task IngestAsync(IngestSource source, IngestRequest request, CancellationToken cancellationToken = default);

    /// <summary>Stores the usable readings and reports the rejected ones instead of failing the batch.</summary>
    Task<IngestBatchResponse> IngestBatchAsync(IngestSource source, IngestBatchRequest request, CancellationToken cancellationToken = default);
}

using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IAlertService
{
    Task<PagedResponse<AlertResponse>> ListAsync(AlertQuery query, CancellationToken cancellationToken = default);
    Task<AlertResponse> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AlertResponse> ResolveAsync(Guid id, CancellationToken cancellationToken = default);
}
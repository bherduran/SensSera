using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<GreenhouseDetailResponse> GetGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default);
}

using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IThresholdService
{
    Task<List<ThresholdResponse>> GetAllByGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default);
    Task<ThresholdResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ThresholdResponse> CreateAsync(ThresholdRequest request, CancellationToken cancellationToken = default);
    Task<ThresholdResponse> UpdateAsync(Guid id, ThresholdRequest request,CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
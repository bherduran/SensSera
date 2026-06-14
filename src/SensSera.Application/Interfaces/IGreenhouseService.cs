using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IGreenhouseService
{
    Task<List<GreenhouseResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GreenhouseResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GreenhouseResponse> CreateAsync(GreenhouseRequest request, CancellationToken cancellationToken = default);
    Task<GreenhouseResponse> UpdateAsync(Guid id, GreenhouseRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
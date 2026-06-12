using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IGreenhouseService
{
    Task<List<GreenhouseResponse>> GetAllAsync();
    Task<GreenhouseResponse> GetByIdAsync(Guid id);
    Task<GreenhouseResponse> CreateAsync(GreenhouseRequest request);
    Task<GreenhouseResponse> UpdateAsync(Guid id, GreenhouseRequest request);
    Task DeleteAsync(Guid id);
}
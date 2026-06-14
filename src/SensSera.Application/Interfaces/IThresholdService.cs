using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IThresholdService
{
    Task<List<ThresholdResponse>> GetAllByGreenhouseAsync(Guid greenhouseId);
    Task<ThresholdResponse> GetByIdAsync(Guid id);
    Task<ThresholdResponse> CreateAsync(ThresholdRequest request);
    Task<ThresholdResponse> UpdateAsync(Guid id, ThresholdRequest request);
    Task DeleteAsync(Guid id);
}
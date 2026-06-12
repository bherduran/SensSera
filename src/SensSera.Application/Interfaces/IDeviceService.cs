using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IDeviceService
{
    Task<List<DeviceResponse>> GetAllByGreenhouseAsync(Guid greenhouseId);
    Task<DeviceResponse> GetByIdAsync(Guid id);
    Task<DeviceWithTokenResponse> CreateAsync(DeviceRequest request);
    Task<DeviceResponse> UpdateAsync(Guid id, DeviceRequest request);
    Task DeleteAsync(Guid id);
    Task<DeviceWithTokenResponse> RotateTokenAsync(Guid id);
}
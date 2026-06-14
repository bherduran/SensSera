using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IDeviceService
{
    Task<List<DeviceResponse>> GetAllByGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default);
    Task<DeviceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeviceWithTokenResponse> CreateAsync(DeviceRequest request, CancellationToken cancellationToken = default);
    Task<DeviceResponse> UpdateAsync(Guid id, DeviceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeviceWithTokenResponse> RotateTokenAsync(Guid id, CancellationToken cancellationToken = default);
}
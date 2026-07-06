using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IReadingService
{
    Task<DeviceReadingsResponse> GetDeviceReadingsAsync(Guid deviceId, DeviceReadingsQuery query, CancellationToken cancellationToken = default);
    Task<GreenhouseReadingsResponse> GetGreenhouseReadingsAsync(Guid greenhouseId, GreenhouseReadingsQuery query, CancellationToken cancellationToken = default);
}

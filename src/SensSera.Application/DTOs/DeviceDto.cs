namespace SensSera.Application.DTOs;

public record DeviceRequest(string Name, Guid GreenhouseId);

public record DeviceResponse(Guid Id, string Name, Guid GreenhouseId, string Status, DateTime CreatedAt);

public record DeviceWithTokenResponse(Guid Id, string Name, Guid GreenhouseId, string Token);

using SensSera.Domain.Enums;

namespace SensSera.Application.DTOs;

public record DeviceRequest(string Name, Guid GreenhouseId, MetricType Metric);

public record DeviceResponse(
    Guid Id,
    string Name,
    Guid GreenhouseId,
    MetricType Metric,
    string Status,
    DateTime CreatedAt);

public record DeviceWithTokenResponse(
    Guid Id,
    string Name,
    Guid GreenhouseId,
    MetricType Metric,
    string Token);

using SensSera.Domain.Enums;

namespace SensSera.Application.DTOs;

public record ThresholdRequest(
    Guid GreenhouseId,
    MetricType Metric,
    double? MinValue,
    double? MaxValue,
    bool IsEnabled);

public record ThresholdResponse(
    Guid Id,
    Guid GreenhouseId,
    MetricType Metric,
    double? MinValue,
    double? MaxValue,
    bool IsEnabled,
    DateTime CreatedAt);

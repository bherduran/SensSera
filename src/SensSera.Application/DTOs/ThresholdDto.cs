namespace SensSera.Application.DTOs;

public record ThresholdRequest(
    Guid GreenhouseId,
    string Metric,
    double? MinValue,
    double? MaxValue,
    bool IsEnabled);

public record ThresholdResponse(
    Guid Id,
    Guid GreenhouseId,
    string Metric,
    double? MinValue,
    double? MaxValue,
    bool IsEnabled,
    DateTime CreatedAt);

    
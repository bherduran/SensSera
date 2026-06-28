namespace SensSera.Application.DTOs;

public sealed record AlertResponse(
    Guid Id, Guid GreenhouseId, Guid DeviceId, Guid ThresholdId,
    string Metric, double TriggeredValue, string Severity, string Status,
    DateTime TriggeredAt, DateTime? ResolvedAt);

public sealed record AlertQuery(string? Status, Guid? GreenhouseId, int Page = 1, int PageSize = 20);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
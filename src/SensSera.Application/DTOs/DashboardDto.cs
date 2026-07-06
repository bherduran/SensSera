namespace SensSera.Application.DTOs;

// GET /api/dashboard — summary across all greenhouses in the tenant
public sealed record DashboardSummaryResponse(IReadOnlyList<GreenhouseSummary> Greenhouses);

public sealed record GreenhouseSummary(
    Guid GreenhouseId,
    string Name,
    int DeviceCount,
    int ActiveAlerts,
    IReadOnlyList<MetricCurrent> Metrics);

public sealed record MetricCurrent(string Metric, double Current);

// GET /api/dashboard/greenhouses/{id} — per-greenhouse detail
public sealed record GreenhouseDetailResponse(
    Guid GreenhouseId,
    string Name,
    string? Location,
    IReadOnlyList<MetricSummary> Metrics,
    IReadOnlyList<AlertResponse> ActiveAlerts);

public sealed record MetricSummary(
    string Metric,
    double Current,
    double Min24h,
    double Max24h,
    double Avg24h);

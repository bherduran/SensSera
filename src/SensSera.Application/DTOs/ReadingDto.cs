namespace SensSera.Application.DTOs;

// GET /api/devices/{id}/readings?from=&to=&limit= — raw time-series for one device
public sealed record DeviceReadingsQuery(DateTime? From, DateTime? To, int Limit = 500);

public sealed record DeviceReadingsResponse(
    Guid DeviceId,
    string Metric,
    IReadOnlyList<ReadingPoint> Readings);

public sealed record ReadingPoint(DateTime RecordedAt, double Value);

// GET /api/greenhouses/{id}/readings?metric=&from=&to=&bucket= — rollup series for one metric
public sealed record GreenhouseReadingsQuery(string? Metric, DateTime? From, DateTime? To, string Bucket = "Hour");

public sealed record GreenhouseReadingsResponse(
    Guid GreenhouseId,
    string Metric,
    string Bucket,
    IReadOnlyList<RollupPoint> Points);

public sealed record RollupPoint(DateTime PeriodStart, double Min, double Max, double Avg, int Count);

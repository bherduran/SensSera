using SensSera.Domain.Enums;

namespace SensSera.Application.DTOs;

// Server -> client SignalR payloads. Shapes mirror the REST DTOs so the
// frontend handles live and fetched data uniformly (§9 of the design doc).
// Enum-typed fields serialize as camelCase via the shared JsonStringEnumConverter.

public sealed record ReadingReceivedEvent(
    Guid GreenhouseId, Guid DeviceId, MetricType Metric, double Value, DateTime RecordedAt);

public sealed record AlertRaisedEvent(
    Guid AlertId, Guid GreenhouseId, MetricType Metric, AlertSeverity Severity, DateTime TriggeredAt);

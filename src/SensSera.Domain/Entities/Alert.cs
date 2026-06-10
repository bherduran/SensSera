using SensSera.Domain.Common;
using SensSera.Domain.Enums;

namespace SensSera.Domain.Entities;

public class Alert : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid GreenhouseId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid ThresholdId { get; set; }
    public MetricType Metric { get; set; }
    public double TriggeredValue { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Greenhouse Greenhouse { get; set; } = null!;
    public Device Device { get; set; } = null!;
    public Threshold Threshold { get; set; } = null!;
}
using SensSera.Domain.Common;
using SensSera.Domain.Enums;

namespace SensSera.Domain.Entities;

public class SensorReading : BaseEntity
{
    public Guid DeviceId { get; set; }
    public Guid OrganizationId { get; set; }
    public MetricType Metric { get; set; }
    public double Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime IngestedAt { get; set; }
    public Device Device { get; set; } = null!;
}
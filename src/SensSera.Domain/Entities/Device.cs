using SensSera.Domain.Common;
using SensSera.Domain.Enums;

namespace SensSera.Domain.Entities;

public class Device : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid GreenhouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MetricType Metric { get; set; }
    public string DeviceTokenHash { get; set; } = string.Empty;
    public DeviceStatus Status { get; set; }
    public DateTime? LastSeenAt { get; set; }
    // Set together: a channel on a node (e.g. "soil1"). Both null for a standalone device with its own token.
    public Guid? NodeId { get; set; }
    public string? Channel { get; set; }
    public Greenhouse Greenhouse { get; set; } = null!;
    public Node? Node { get; set; }
    public ICollection<SensorReading> Readings { get; set; } = [];
    public ICollection<ReadingRollup> Rollups { get; set; } = [];
}

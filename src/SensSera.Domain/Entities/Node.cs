using SensSera.Domain.Common;

namespace SensSera.Domain.Entities;

/// <summary>
/// A physical board (e.g. an ESP32) that authenticates with one token and reports several
/// sensors. Each sensor is a <see cref="Device"/> linked to the node by a channel name.
/// </summary>
public class Node : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid GreenhouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string? FirmwareVersion { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public Greenhouse Greenhouse { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = [];
}

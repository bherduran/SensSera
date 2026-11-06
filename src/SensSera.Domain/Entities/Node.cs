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
    // Last time the API stored part of a batch and skipped the rest, and why (shown on the node card).
    public DateTime? LastRejectedAt { get; set; }
    public string? LastRejectReason { get; set; }
    public Greenhouse Greenhouse { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = [];
}

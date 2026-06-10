using SensSera.Domain.Common;

namespace SensSera.Domain.Entities;

public class Greenhouse : BaseEntity
{
    public Guid OrganizationId { get; set;}
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public double? AreaM2 { get; set; }
    public string? CropType { get; set; }
    public Organization Organization { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = [];
    public ICollection<Threshold> Thresholds { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];
   

}
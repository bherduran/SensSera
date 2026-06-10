using SensSera.Domain.Common;               
using SensSera.Domain.Enums;                                                                                                                           
                                          
namespace SensSera.Domain.Entities;

public class Threshold : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid GreenhouseId { get; set; }
    public MetricType Metric { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public bool IsEnabled { get; set; }
    public Greenhouse Greenhouse { get; set; } = null!;
 
}
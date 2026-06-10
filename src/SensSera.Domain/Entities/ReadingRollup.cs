using SensSera.Domain.Common;                                                                                                                          
using SensSera.Domain.Enums;                                                                                                                           
                                                                                                                                                         
namespace SensSera.Domain.Entities;                                                                                                                    
                                                                     
public class ReadingRollup : BaseEntity                    
{                                  
    public Guid DeviceId { get; set; }
    public MetricType Metric { get; set; }
    public RollupBucket Bucket { get; set; }
    public DateTime PeriodStart { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Avg { get; set; }
    public int Count { get; set; }
    public Device Device { get; set; } = null!;
}
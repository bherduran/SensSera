using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence.Configurations;

public class ReadingRollupConfiguration : IEntityTypeConfiguration<ReadingRollup>
{
    public void Configure(EntityTypeBuilder<ReadingRollup> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.OrganizationId, r.DeviceId, r.Metric, r.Bucket, r.PeriodStart}).IsUnique();


        builder.HasOne(r => r.Device)
            .WithMany(d => d.Rollups)
            .HasForeignKey(r => r.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);    
 
    }
}
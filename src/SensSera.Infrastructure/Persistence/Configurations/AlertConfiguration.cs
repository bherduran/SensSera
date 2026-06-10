using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.OrganizationId, a.Status});

     
        builder.HasOne(a => a.Greenhouse)
            .WithMany(g => g.Alerts)
            .HasForeignKey(a => a.GreenhouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Device)
            .WithMany()
            .HasForeignKey(a => a.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);    

         builder.HasOne(a => a.Threshold)
            .WithMany()
            .HasForeignKey(a => a.ThresholdId)
            .OnDelete(DeleteBehavior.Restrict);    
    }
}
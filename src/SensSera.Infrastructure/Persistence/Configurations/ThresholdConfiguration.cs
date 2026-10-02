using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence.Configurations;

public class ThresholdConfiguration : IEntityTypeConfiguration<Threshold>
{
    public void Configure(EntityTypeBuilder<Threshold> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => new { t.GreenhouseId, t.Metric }).IsUnique();

        builder.HasOne(t => t.Greenhouse)
            .WithMany(g => g.Thresholds)
            .HasForeignKey(t => t.GreenhouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence.Configurations;

public class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.DeviceId, s.RecordedAt});
        builder.HasIndex(s => new { s.OrganizationId, s.Metric ,s.RecordedAt});

        builder.HasOne(s => s.Device)
            .WithMany(d => d.Readings)
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
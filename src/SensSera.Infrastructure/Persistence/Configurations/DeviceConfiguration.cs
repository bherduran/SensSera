using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.DeviceTokenHash).IsRequired();
        
        builder.HasOne(d => d.Greenhouse)
            .WithMany(g => g.Devices)
            .HasForeignKey(d => d.GreenhouseId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence.Configurations;

public class GreenhouseConfiguration : IEntityTypeConfiguration<Greenhouse>
{
    public void Configure(EntityTypeBuilder<Greenhouse> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(g => g.Organization)
        .WithMany(o => o.Greenhouses)
        .HasForeignKey(g => g.OrganizationId)
        .OnDelete(DeleteBehavior.Cascade);
    }
}
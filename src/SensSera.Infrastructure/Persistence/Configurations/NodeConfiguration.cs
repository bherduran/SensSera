using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence.Configurations;

public class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Name).IsRequired().HasMaxLength(200);
        builder.Property(n => n.TokenHash).IsRequired();
        builder.Property(n => n.FirmwareVersion).HasMaxLength(50);

        builder.HasOne(n => n.Greenhouse)
            .WithMany(g => g.Nodes)
            .HasForeignKey(n => n.GreenhouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

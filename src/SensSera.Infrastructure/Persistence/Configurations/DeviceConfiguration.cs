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

        builder.Property(d => d.Channel).HasMaxLength(32);

        // NO ACTION, not RESTRICT: the check runs at the end of the statement, so a greenhouse delete
        // that cascades to both the node and its channel devices succeeds, while deleting a node that
        // still has channels fails. Channel devices carry readings and alerts and must go through
        // the device delete path, never a silent cascade.
        builder.HasOne(d => d.Node)
            .WithMany(n => n.Devices)
            .HasForeignKey(d => d.NodeId)
            .OnDelete(DeleteBehavior.NoAction);

        // One device per channel on a node. Standalone devices (NodeId null) are not constrained,
        // since Postgres treats nulls as distinct in a unique index.
        builder.HasIndex(d => new { d.NodeId, d.Channel }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Devices_NodeChannel", "(\"NodeId\" IS NULL) = (\"Channel\" IS NULL)"));
    }
}
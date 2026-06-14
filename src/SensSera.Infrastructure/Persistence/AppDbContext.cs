using Microsoft.EntityFrameworkCore;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext? _tenant;
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Greenhouse> Greenhouses => Set<Greenhouse>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<Threshold> Thresholds => Set<Threshold>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<ReadingRollup> ReadingRollups => Set<ReadingRollup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Greenhouse>().HasQueryFilter(g => _tenant!.OrganizationId != null && g.OrganizationId == _tenant.OrganizationId);
        modelBuilder.Entity<Device>().HasQueryFilter(d => _tenant!.OrganizationId != null && d.OrganizationId == _tenant.OrganizationId);
        modelBuilder.Entity<Threshold>().HasQueryFilter(t => _tenant!.OrganizationId != null && t.OrganizationId == _tenant.OrganizationId);
        modelBuilder.Entity<Alert>().HasQueryFilter(a => _tenant!.OrganizationId != null && a.OrganizationId == _tenant.OrganizationId);
        modelBuilder.Entity<SensorReading>().HasQueryFilter(s => _tenant!.OrganizationId != null && s.OrganizationId == _tenant.OrganizationId);
        modelBuilder.Entity<User>().HasQueryFilter(u => _tenant!.OrganizationId != null && u.OrganizationId == _tenant.OrganizationId);

    }
    
}


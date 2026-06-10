using Microsoft.EntityFrameworkCore;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
    }
    
}


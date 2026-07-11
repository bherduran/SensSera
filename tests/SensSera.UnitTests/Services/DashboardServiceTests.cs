using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;
using SensSera.Infrastructure.Services;

namespace SensSera.UnitTests.Services;

public class DashboardServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IDashboardService _sut;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenant = new FakeDashboardTenant(_orgId);
        _db = new AppDbContext(options, tenant);
        _sut = new DashboardService(_db, tenant, new FixedClock(_now));
    }

    [Fact]
    public async Task GetGreenhouseAsync_ComputesWeightedAvgAndCurrentFromLatestBucket()
    {
        var gh = new Greenhouse { OrganizationId = _orgId, Name = "GH1", CreatedAt = _now, UpdatedAt = _now };
        var device = new Device { OrganizationId = _orgId, Greenhouse = gh, Name = "Temp", Metric = MetricType.Temperature, CreatedAt = _now, UpdatedAt = _now };
        _db.Greenhouses.Add(gh);
        _db.Devices.Add(device);
        _db.ReadingRollups.AddRange(
            Rollup(device, _now.AddHours(-2), min: 20, max: 24, avg: 22, count: 10),
            Rollup(device, _now.AddHours(-1), min: 22, max: 30, avg: 26, count: 30)); // latest
        await _db.SaveChangesAsync();

        var result = await _sut.GetGreenhouseAsync(gh.Id);

        result.Metrics.Should().ContainSingle();
        var m = result.Metrics[0];
        m.Metric.Should().Be(MetricType.Temperature);
        m.Current.Should().Be(26);   // latest bucket avg
        m.Min24h.Should().Be(20);
        m.Max24h.Should().Be(30);
        m.Avg24h.Should().Be(25);    // (22*10 + 26*30) / 40 — count-weighted, not (22+26)/2
    }

    [Fact]
    public async Task GetGreenhouseAsync_CrossTenant_ThrowsKeyNotFound()
    {
        var otherGh = new Greenhouse { OrganizationId = Guid.NewGuid(), Name = "Other", CreatedAt = _now, UpdatedAt = _now };
        _db.Greenhouses.Add(otherGh);
        await _db.SaveChangesAsync();
        var otherId = _db.Greenhouses.IgnoreQueryFilters().First().Id;

        await _sut.Invoking(s => s.GetGreenhouseAsync(otherId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsActiveAlertCountAndCurrentPerMetric()
    {
        var gh = new Greenhouse { OrganizationId = _orgId, Name = "GH1", CreatedAt = _now, UpdatedAt = _now };
        var device = new Device { OrganizationId = _orgId, Greenhouse = gh, Name = "Temp", Metric = MetricType.Temperature, CreatedAt = _now, UpdatedAt = _now };
        _db.Greenhouses.Add(gh);
        _db.Devices.Add(device);
        _db.ReadingRollups.Add(Rollup(device, _now.AddHours(-1), min: 22, max: 30, avg: 26, count: 30));
        _db.Alerts.AddRange(
            new Alert { OrganizationId = _orgId, GreenhouseId = gh.Id, DeviceId = device.Id, Status = AlertStatus.Open, TriggeredAt = _now, CreatedAt = _now, UpdatedAt = _now },
            new Alert { OrganizationId = _orgId, GreenhouseId = gh.Id, DeviceId = device.Id, Status = AlertStatus.Resolved, TriggeredAt = _now, CreatedAt = _now, UpdatedAt = _now });
        await _db.SaveChangesAsync();

        var result = await _sut.GetSummaryAsync();

        var summary = result.Greenhouses.Should().ContainSingle().Subject;
        summary.DeviceCount.Should().Be(1);
        summary.ActiveAlerts.Should().Be(1); // Resolved is excluded
        summary.Metrics.Should().ContainSingle();
        summary.Metrics[0].Current.Should().Be(26);
    }

    private ReadingRollup Rollup(Device d, DateTime periodStart, double min, double max, double avg, int count) =>
        new()
        {
            OrganizationId = _orgId,
            Device = d,
            Metric = d.Metric,
            Bucket = RollupBucket.Hour,
            PeriodStart = periodStart,
            Min = min,
            Max = max,
            Avg = avg,
            Count = count,
            CreatedAt = _now,
            UpdatedAt = _now,
        };

    public void Dispose() => _db.Dispose();
}

file sealed class FakeDashboardTenant(Guid orgId) : ITenantContext
{
    public Guid? OrganizationId => orgId;
}

file sealed class FixedClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}

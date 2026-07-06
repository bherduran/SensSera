using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;
using SensSera.Infrastructure.Services;

namespace SensSera.UnitTests.Services;

public class ReadingServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IReadingService _sut;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    public ReadingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenant = new FakeReadingTenant(_orgId);
        _db = new AppDbContext(options, tenant);
        _sut = new ReadingService(_db, tenant, new ReadingFixedClock(_now));
    }

    [Fact]
    public async Task GetDeviceReadingsAsync_RespectsLimitAndOrdersDescending()
    {
        var device = new Device { OrganizationId = _orgId, GreenhouseId = Guid.NewGuid(), Name = "Temp", Metric = MetricType.Temperature, CreatedAt = _now, UpdatedAt = _now };
        _db.Devices.Add(device);
        _db.SensorReadings.AddRange(
            Reading(device, _now.AddMinutes(-30), 21),
            Reading(device, _now.AddMinutes(-20), 22),
            Reading(device, _now.AddMinutes(-10), 23)); // newest
        await _db.SaveChangesAsync();

        var result = await _sut.GetDeviceReadingsAsync(device.Id, new DeviceReadingsQuery(null, null, Limit: 2));

        result.Metric.Should().Be("Temperature");
        result.Readings.Should().HaveCount(2);
        result.Readings[0].Value.Should().Be(23); // most recent first
        result.Readings[1].Value.Should().Be(22);
    }

    [Fact]
    public async Task GetDeviceReadingsAsync_UnknownDevice_ThrowsKeyNotFound()
    {
        await _sut.Invoking(s => s.GetDeviceReadingsAsync(Guid.NewGuid(), new DeviceReadingsQuery(null, null)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetGreenhouseReadingsAsync_MissingMetric_ThrowsArgumentException()
    {
        var gh = new Greenhouse { OrganizationId = _orgId, Name = "GH1", CreatedAt = _now, UpdatedAt = _now };
        _db.Greenhouses.Add(gh);
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.GetGreenhouseReadingsAsync(gh.Id, new GreenhouseReadingsQuery(Metric: null, null, null)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetGreenhouseReadingsAsync_CombinesDevicesOfSameMetricPerBucket()
    {
        var gh = new Greenhouse { OrganizationId = _orgId, Name = "GH1", CreatedAt = _now, UpdatedAt = _now };
        var d1 = new Device { OrganizationId = _orgId, Greenhouse = gh, Name = "T1", Metric = MetricType.Temperature, CreatedAt = _now, UpdatedAt = _now };
        var d2 = new Device { OrganizationId = _orgId, Greenhouse = gh, Name = "T2", Metric = MetricType.Temperature, CreatedAt = _now, UpdatedAt = _now };
        _db.Greenhouses.Add(gh);
        _db.Devices.AddRange(d1, d2);
        var bucket = _now.AddHours(-1);
        _db.ReadingRollups.AddRange(
            Rollup(d1, bucket, min: 20, max: 24, avg: 22, count: 10),
            Rollup(d2, bucket, min: 18, max: 28, avg: 24, count: 10));
        await _db.SaveChangesAsync();

        var result = await _sut.GetGreenhouseReadingsAsync(gh.Id, new GreenhouseReadingsQuery("Temperature", null, null));

        result.Metric.Should().Be("Temperature");
        var point = result.Points.Should().ContainSingle().Subject;
        point.Min.Should().Be(18);
        point.Max.Should().Be(28);
        point.Avg.Should().Be(23);   // (22*10 + 24*10) / 20
        point.Count.Should().Be(20);
    }

    private SensorReading Reading(Device d, DateTime recordedAt, double value) =>
        new()
        {
            OrganizationId = _orgId,
            Device = d,
            Metric = d.Metric,
            Value = value,
            RecordedAt = recordedAt,
            IngestedAt = recordedAt,
            CreatedAt = recordedAt,
            UpdatedAt = recordedAt,
        };

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

file sealed class FakeReadingTenant(Guid orgId) : ITenantContext
{
    public Guid? OrganizationId => orgId;
}

file sealed class ReadingFixedClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}

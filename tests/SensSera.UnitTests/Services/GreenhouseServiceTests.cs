using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Infrastructure.Persistence;
using SensSera.Infrastructure.Services;

namespace SensSera.UnitTests.Services;

public class GreenhouseServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IGreenhouseService _sut;
    private readonly Guid _orgId = Guid.NewGuid();

    public GreenhouseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        var tenant = new FakeTenantContext(_orgId);
        _sut = new GreenhouseService(_db, tenant);    
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyOwnOrg()
    {
        _db.Greenhouses.AddRange(
            new Greenhouse { OrganizationId = _orgId, Name = "Mine", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow},
            new Greenhouse { OrganizationId = Guid.NewGuid(), Name = "Other", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow}
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();
        
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Mine");
    }

    [Fact]
    public async Task GetByIdAsync_WrongOrg_ThrowsKeyNotFoundException()
    {
        var otherId = Guid.NewGuid();
        _db.Greenhouses.Add(new Greenhouse { OrganizationId = otherId, Name = "Other", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow});
        await _db.SaveChangesAsync();

        var id = _db.Greenhouses.First().Id;

        await _sut.Invoking(s => s.GetByIdAsync(id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_PersistGreenhouse()
    {
        var result = await _sut.CreateAsync(new GreenhouseRequest("Test GH", "Anamur"));

        result.Id.Should().NotBeEmpty();
        _db.Greenhouses.Should().HaveCount(1);
    }

    public void Dispose() => _db.Dispose();

}

file sealed class FakeTenantContext(Guid orgId) : ITenantContext
{
    public Guid OrganizationId => orgId;
}

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;
using SensSera.Infrastructure.Services;

namespace SensSera.UnitTests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options, new FakeNullTenantContext());
        _sut = new AuthService(_db, new FakePasswordHasher(), new FakeJwtService(), TimeProvider.System);
    }

    [Fact]
    public async Task RegisterAsync_CreatesOrgAndAdminUserAtomically()
    {
        var (response, refreshToken) = await _sut.RegisterAsync(
            new RegisterRequest("My Org", "admin@my.com", "password123"));

        _db.Organizations.Should().HaveCount(1);
        _db.Users.IgnoreQueryFilters().Should().HaveCount(1);
        _db.RefreshTokens.Should().HaveCount(1);

        var user = await _db.Users.IgnoreQueryFilters().SingleAsync();
        user.Role.Should().Be(Role.Admin);
        user.Email.Should().Be("admin@my.com");
        refreshToken.Should().NotBeNullOrEmpty();
        response.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshAsync_RotatesToken_RevokesOldAndIssuesNew()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Email = "u@x.com",
            PasswordHash = "x",
            Role = Role.Admin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        const string raw = "raw-refresh-token";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        var oldToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(oldToken);
        await _db.SaveChangesAsync();

        await _sut.RefreshAsync(raw);

        var stored = await _db.RefreshTokens.AsNoTracking().ToListAsync();
        stored.Should().HaveCount(2);

        var revoked = stored.Single(t => t.Id == oldToken.Id);
        revoked.RevokedAt.Should().NotBeNull();

        var newToken = stored.Single(t => t.Id != oldToken.Id);
        newToken.ReplacedByTokenId.Should().Be(oldToken.Id);
        newToken.RevokedAt.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}

file sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";
    public bool Verify(string password, string hash) => hash == $"hashed:{password}";
}

file sealed class FakeJwtService : IJwtService
{
    public string GenerateAccessToken(User user) => $"access-{user.Id}";
    public string GenerateRefreshToken() => Guid.NewGuid().ToString("N");
}

file sealed class FakeNullTenantContext : ITenantContext
{
    public Guid? OrganizationId => null;
}
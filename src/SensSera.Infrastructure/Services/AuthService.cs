using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtService jwtService,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<(LoginResponse Response, string RefreshToken)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email, cancellationToken))
            throw new ArgumentException("Email already in use");

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.OrganizationName,
            Slug = request.OrganizationName.ToLowerInvariant().Replace(" ", "-"),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = Role.Admin,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        db.Organizations.Add(org);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        var (accessToken, refreshToken) = await IssueTokensAsync(user, cancellationToken: cancellationToken);
        return (new LoginResponse(accessToken, 900, new UserInfo(user.Id, user.Email, user.Role.ToString())), refreshToken);
    }

    public async Task<(LoginResponse Response, string RefreshToken)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        var (accessToken, refreshToken) = await IssueTokensAsync(user, cancellationToken: cancellationToken);
        return (new LoginResponse(accessToken, 900, new UserInfo(user.Id, user.Email, user.Role.ToString())), refreshToken);
    }

    public async Task<(RefreshResponse Response, string RefreshToken)> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        // A revoked token coming back means it was copied: either the thief or the real user is
        // now holding a stale one. We can't tell which, so end every session for this user.
        if (stored.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(stored.UserId, cancellationToken);
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        if (stored.ExpiresAt < timeProvider.GetUtcNow().UtcDateTime)
        {
            stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        // rotate: revoke old, issue new
        stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        var (accessToken, newRefreshToken) = await IssueTokensAsync(stored.User, stored.Id, cancellationToken);
        return (new RefreshResponse(accessToken, 900), newRefreshToken);
    }

    private async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var active = await db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
            token.RevokedAt = now;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null, cancellationToken);

        if (stored is null) return;

        stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MeResponse> MeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found");
        return new MeResponse(user.Id, user.Email, user.Role.ToString(), user.OrganizationId);
    }

    private async Task<(string accessToken, string refreshToken)> IssueTokensAsync(User user, Guid? replacesId = null, CancellationToken cancellationToken = default)
    {
        var rawRefresh = jwtService.GenerateRefreshToken();
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawRefresh),
            ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(7),
            ReplacedByTokenId = replacesId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync(cancellationToken);

        return (jwtService.GenerateAccessToken(user), rawRefresh);
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
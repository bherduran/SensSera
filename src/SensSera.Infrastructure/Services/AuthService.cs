using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public class AuthService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtService jwtService,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<(LoginResponse Response, string RefreshToken)> RegisterAsync(RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
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
        await db.SaveChangesAsync();

        var (accessToken, refreshToken) = await IssueTokensAsync(user);
        return (new LoginResponse(accessToken, 900, new UserInfo(user.Id, user.Email, user.Role.ToString())), refreshToken);
    }

    public async Task<(LoginResponse Response, string RefreshToken)> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        var (accessToken, refreshToken) = await IssueTokensAsync(user);
        return (new LoginResponse(accessToken, 900, new UserInfo(user.Id, user.Email, user.Role.ToString())), refreshToken);
    }

    public async Task<RefreshResponse> RefreshAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        if (stored.ExpiresAt < timeProvider.GetUtcNow().UtcDateTime)
        {
            stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        // rotate: revoke old, issue new
        stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        var (accessToken, _) = await IssueTokensAsync(stored.User, stored.Id);
        return new RefreshResponse(accessToken, 900);    
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null);

        if (stored is null) return;

        stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();    
    }

    public async Task<MeResponse> MeAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        return new MeResponse(user.Id, user.Email, user.Role.ToString(), user.OrganizationId);  
    }

    private async Task<(string accessToken, string refreshToken)> IssueTokensAsync(User user, Guid? replacesId = null)
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
        await db.SaveChangesAsync();

        return (jwtService.GenerateAccessToken(user), rawRefresh);
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
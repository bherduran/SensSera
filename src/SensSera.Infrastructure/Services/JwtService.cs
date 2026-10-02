using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SensSera.Application.Interfaces;
using SensSera.Application.Options;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Services;

public sealed class JwtService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtService
{
    private readonly JwtOptions _opts = options.Value;
    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new []
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("org_id", user.OrganizationId.ToString()),
            new Claim("role", user.Role.ToString()),
           
        };

        var token = new JwtSecurityToken(   
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims, 
            expires: timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15),
        signingCredentials: creds);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
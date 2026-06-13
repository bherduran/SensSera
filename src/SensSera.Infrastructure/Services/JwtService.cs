using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;

namespace SensSera.Infrastructure.Services;

public class JwtService(IConfiguration config) : IJwtService
{
    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new []
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("org_id", user.OrganizationId.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
           
        };

        var token = new JwtSecurityToken(   
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims, 
            expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: creds);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
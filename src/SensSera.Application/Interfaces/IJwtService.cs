using SensSera.Domain.Entities;

namespace SensSera.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}


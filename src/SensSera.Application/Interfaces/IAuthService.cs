using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IAuthService
{
    Task<(LoginResponse Response, string RefreshToken)> RegisterAsync(RegisterRequest request);
    Task<(LoginResponse Response, string RefreshToken)> LoginAsync(LoginRequest request);
    Task<RefreshResponse> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<MeResponse> MeAsync(Guid userId);
}
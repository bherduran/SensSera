using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface IAuthService
{
    Task<(LoginResponse Response, string RefreshToken)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<(LoginResponse Response, string RefreshToken)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<(RefreshResponse Response, string RefreshToken)> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<MeResponse> MeAsync(Guid userId, CancellationToken cancellationToken = default);
}
namespace SensSera.Application.DTOs;

public record RegisterRequest(string OrganizationName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, int ExpiresIn, UserInfo User);
public record UserInfo(Guid Id, string Email, string Role);
public record RefreshResponse(string AccessToken, int ExpiresIn);
public record MeResponse(Guid Id, string Email, string Role, Guid OrganizationId);
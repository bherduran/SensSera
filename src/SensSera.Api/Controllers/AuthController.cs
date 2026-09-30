using Microsoft.AspNetCore.Authorization;                
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SensSera.Application.DTOs;              
using SensSera.Application.Interfaces; 

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{   
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var (response, refreshToken) = await authService.RegisterAsync(request, cancellationToken);
        SetRefreshTokenCookie(refreshToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var (response, refreshToken) = await authService.LoginAsync(request, cancellationToken);
        SetRefreshTokenCookie(refreshToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh(CancellationToken cancellationToken)
    {
        var token = Request.Cookies["refreshToken"]
            ?? throw new UnauthorizedAccessException("Refresh token missing");
        var (response, newRefreshToken) = await authService.RefreshAsync(token, cancellationToken);
        SetRefreshTokenCookie(newRefreshToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = Request.Cookies["refreshToken"];
        if (token is not null)
            await authService.LogoutAsync(token, cancellationToken);
        Response.Cookies.Delete("refreshToken");
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();
        var response = await authService.MeAsync(userId, cancellationToken);
        return Ok(response);
    }

    private void SetRefreshTokenCookie(string token)
    {   
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        Response.Cookies.Append("refreshToken", token, new CookieOptions
        {
            HttpOnly = true,    
            Secure = !env.IsDevelopment(),
            SameSite = env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        });
    }


}
using Microsoft.AspNetCore.Authorization;                
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SensSera.Application.DTOs;              
using SensSera.Application.Interfaces; 

namespace SensSera.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{   
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (response, refreshToken) = await authService.RegisterAsync(request);
        SetRefreshTokenCookie(refreshToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (response, refreshToken) = await authService.LoginAsync(request);
        SetRefreshTokenCookie(refreshToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var token = Request.Cookies["refreshToken"]
            ?? throw new UnauthorizedAccessException("Refresh token missing");
        var response = await authService.RefreshAsync(token);
        return Ok(response);    
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var token = Request.Cookies["refreshToken"];
        if (token is not null)
            await authService.LogoutAsync(token);
        Response.Cookies.Delete("refreshToken");
        return NoContent();    
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();
        var response = await authService.MeAsync(userId);
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
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Api.Auth;

public class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(!Request.Headers.TryGetValue("X-Device-Token", out var tokenValue))
            return AuthenticateResult.NoResult();

        var raw = tokenValue.ToString();
        var parts = raw.Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var deviceId))
            return AuthenticateResult.Fail("Invalid token format");

        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parts[1])));

        var device = await db.Devices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.DeviceTokenHash == secretHash);

        if (device is null)
            return AuthenticateResult.Fail("Invalid device token");

        var claims = new []
        {
            new Claim("device_id", device.Id.ToString()),
            new Claim("org_id", device.OrganizationId.ToString()),
            new Claim("greenhouse_id", device.GreenhouseId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);                
    }
}    
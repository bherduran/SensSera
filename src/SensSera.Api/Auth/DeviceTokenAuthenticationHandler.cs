using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Api.Auth;

/// <summary>
/// Authenticates <c>X-Device-Token: {id}.{secret}</c>. The id is looked up as a standalone device
/// first, then as a node (a board with several channels); only the SHA-256 of the secret is stored.
/// A device emits a <c>device_id</c> claim, a node a <c>node_id</c> claim.
/// </summary>
public sealed class DeviceTokenAuthenticationHandler(
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
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var id))
            return AuthenticateResult.Fail("Invalid token format");

        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parts[1])));
        var ct = Context.RequestAborted;

        // Channel devices (NodeId set) have no usable token of their own; they report through their node.
        var device = await db.Devices
            .IgnoreQueryFilters()
            .Where(d => d.Id == id && d.NodeId == null)
            .Select(d => new { d.DeviceTokenHash, d.OrganizationId, d.GreenhouseId })
            .FirstOrDefaultAsync(ct);
        if (device is not null)
            return HashMatches(device.DeviceTokenHash, secretHash)
                ? Success("device_id", id, device.OrganizationId, device.GreenhouseId)
                : AuthenticateResult.Fail("Invalid device token");

        var node = await db.Nodes
            .IgnoreQueryFilters()
            .Where(n => n.Id == id)
            .Select(n => new { n.TokenHash, n.OrganizationId, n.GreenhouseId })
            .FirstOrDefaultAsync(ct);
        if (node is not null && HashMatches(node.TokenHash, secretHash))
            return Success("node_id", id, node.OrganizationId, node.GreenhouseId);

        return AuthenticateResult.Fail("Invalid device token");
    }

    // Constant-time, so response timing does not reveal how much of the hash matched.
    private static bool HashMatches(string stored, string computed) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(computed));

    private static AuthenticateResult Success(string idClaim, Guid id, Guid organizationId, Guid greenhouseId)
    {
        var claims = new[]
        {
            new Claim(idClaim, id.ToString()),
            new Claim("org_id", organizationId.ToString()),
            new Claim("greenhouse_id", greenhouseId.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SensSera.IntegrationTests;

/// <summary>
/// Refresh-cookie flow. Cookies are handled by hand (read Set-Cookie, send Cookie) so a test can
/// replay an old token the way an attacker holding a stolen cookie would.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthFlowTests(SensSeraApiFactory factory)
{
    [Fact]
    public async Task Refresh_RotatesCookie_SoConsecutiveRefreshesKeepWorking()
    {
        var first = await RegisterAsync();

        var (status1, second) = await RefreshAsync(first);
        status1.Should().Be(HttpStatusCode.OK);
        second.Should().NotBeNull().And.NotBe(first, "the rotated token must be written back to the cookie");

        var (status2, _) = await RefreshAsync(second!);
        status2.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReusingRevokedRefreshToken_RevokesTheWholeFamily()
    {
        var stolen = await RegisterAsync();

        // Legitimate client rotates; the old token is now revoked.
        var (_, current) = await RefreshAsync(stolen);

        // Replay of the revoked token is the theft signal → rejected, and every live token dies too.
        (await RefreshAsync(stolen)).Status.Should().Be(HttpStatusCode.Unauthorized);
        (await RefreshAsync(current!)).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Cookies are Secure outside Development, so talk https; cookies are managed manually.
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
    });

    private async Task<string> RegisterAsync()
    {
        var response = await Client().PostAsJsonAsync("/api/auth/register", new
        {
            organizationName = "Auth Org",
            email = $"{Guid.NewGuid():N}@it.test",
            password = "Passw0rd!123",
        });
        response.EnsureSuccessStatusCode();
        return RefreshCookie(response) ?? throw new InvalidOperationException("register set no refresh cookie");
    }

    private async Task<(HttpStatusCode Status, string? NewToken)> RefreshAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"refreshToken={token}");
        var response = await Client().SendAsync(request);
        return (response.StatusCode, RefreshCookie(response));
    }

    private static string? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Where(c => c.StartsWith("refreshToken=", StringComparison.Ordinal))
                .Select(c => c["refreshToken=".Length..].Split(';')[0])
                .FirstOrDefault(v => v.Length > 0)
            : null;
}

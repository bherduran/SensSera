using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace SensSera.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApiFlowTests(SensSeraApiFactory factory)
{
    [Fact]
    public async Task Ingest_ThresholdBreach_CreatesAlertAndPushesAlertRaised()
    {
        var (client, token) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (deviceId, deviceToken) = await CreateDeviceAsync(client, greenhouseId, "Temperature");
        (await client.PostAsJsonAsync("/api/thresholds",
            new { greenhouseId, metric = "Temperature", minValue = 10, maxValue = 30, isEnabled = true }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        await using var hub = await ConnectHubAsync(token);
        var alertRaised = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>("AlertRaised", e => alertRaised.TrySetResult(e));

        (await IngestAsync(deviceToken, "Temperature", 41, DateTimeOffset.UtcNow.ToString("O")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The evaluation job runs every second in tests; the push proves job → DB → SignalR.
        var pushed = await alertRaised.Task.WaitAsync(TimeSpan.FromSeconds(20));
        pushed.GetProperty("greenhouseId").GetGuid().Should().Be(greenhouseId);

        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
        var item = alerts.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("deviceId").GetGuid().Should().Be(deviceId);
        item.GetProperty("triggeredValue").GetDouble().Should().Be(41);
        item.GetProperty("status").GetString().Should().Be("open");
    }

    [Theory]
    [InlineData("2026-09-30T10:00:00+03:00")] // offset → deserialized as Kind=Local
    [InlineData("2026-09-30T10:00:00")]       // no offset → Kind=Unspecified
    [InlineData("2026-09-30T10:00:00Z")]
    public async Task Ingest_AcceptsAnyTimestampFormat_AndStoresUtc(string recordedAt)
    {
        var (client, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (deviceId, deviceToken) = await CreateDeviceAsync(client, greenhouseId, "Humidity");

        (await IngestAsync(deviceToken, "Humidity", 55, recordedAt))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var readings = await client.GetFromJsonAsync<JsonElement>(
            $"/api/devices/{deviceId}/readings?from=2026-09-29T00:00:00Z&to=2026-10-01T00:00:00Z");
        var stored = readings.GetProperty("readings").EnumerateArray().Should().ContainSingle().Subject;
        var expected = DateTimeOffset.Parse(recordedAt,
            styles: System.Globalization.DateTimeStyles.AssumeUniversal).UtcDateTime;
        stored.GetProperty("recordedAt").GetDateTime().ToUniversalTime().Should().Be(expected);
    }

    [Fact]
    public async Task IngestBatch_WritesEveryReading()
    {
        var (client, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (deviceId, deviceToken) = await CreateDeviceAsync(client, greenhouseId, "Co2");
        var now = DateTime.UtcNow;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest/batch")
        {
            Content = JsonContent.Create(new
            {
                readings = Enumerable.Range(0, 5)
                    .Select(i => new { metric = "Co2", value = 800.0 + i, recordedAt = now.AddSeconds(-i) }),
            }),
        };
        request.Headers.Add("X-Device-Token", deviceToken);

        (await factory.CreateClient().SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var readings = await client.GetFromJsonAsync<JsonElement>($"/api/devices/{deviceId}/readings");
        readings.GetProperty("readings").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task IngestBatch_WithOneInvalidReading_RejectsWholeBatch()
    {
        var (client, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (deviceId, deviceToken) = await CreateDeviceAsync(client, greenhouseId, "Humidity");
        var now = DateTime.UtcNow;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest/batch")
        {
            Content = JsonContent.Create(new
            {
                readings = new[]
                {
                    new { metric = "Humidity", value = 50.0, recordedAt = now },
                    new { metric = "Humidity", value = 500.0, recordedAt = now }, // out of 0-100 range
                },
            }),
        };
        request.Headers.Add("X-Device-Token", deviceToken);

        (await factory.CreateClient().SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var readings = await client.GetFromJsonAsync<JsonElement>($"/api/devices/{deviceId}/readings");
        readings.GetProperty("readings").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task OtherTenant_GetsNotFound_AndEmptyList()
    {
        var (orgA, _) = await RegisterAsync();
        var (orgB, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(orgA);

        (await orgB.GetAsync($"/api/greenhouses/{greenhouseId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await orgB.GetFromJsonAsync<JsonElement>("/api/greenhouses")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Ingest_WithInvalidDeviceToken_Returns401()
    {
        var response = await IngestAsync($"{Guid.NewGuid()}.not-a-real-secret", "Temperature", 20, DateTimeOffset.UtcNow.ToString("O"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutJwt_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/greenhouses");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Alerts hold RESTRICT FKs to their device and threshold; deletes must clear them, not fail.
    [Fact]
    public async Task DeleteDevice_WithAlerts_Succeeds()
    {
        var (client, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (deviceId, deviceToken) = await CreateDeviceAsync(client, greenhouseId, "Temperature");
        await RaiseAlertAsync(client, greenhouseId, deviceToken);

        (await client.DeleteAsync($"/api/devices/{deviceId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetFromJsonAsync<JsonElement>("/api/alerts")).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task DeleteGreenhouse_WithAlerts_Succeeds()
    {
        var (client, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (_, deviceToken) = await CreateDeviceAsync(client, greenhouseId, "Temperature");
        await RaiseAlertAsync(client, greenhouseId, deviceToken);

        (await client.DeleteAsync($"/api/greenhouses/{greenhouseId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/greenhouses/{greenhouseId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SecondThresholdForSameMetric_Returns409()
    {
        var (client, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var threshold = new { greenhouseId, metric = "Humidity", minValue = 40, maxValue = 80, isEnabled = true };

        (await client.PostAsJsonAsync("/api/thresholds", threshold)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/api/thresholds", threshold)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RegisterWithTakenEmail_Returns409()
    {
        var body = new { organizationName = "Dup Org", email = $"{Guid.NewGuid():N}@it.test", password = "Passw0rd!123" };

        (await factory.CreateClient().PostAsJsonAsync("/api/auth/register", body)).EnsureSuccessStatusCode();
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/register", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Device_StatusSerializesAsCamelCaseEnum()
    {
        var (client, _) = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        await CreateDeviceAsync(client, greenhouseId, "Light");

        var devices = await client.GetFromJsonAsync<JsonElement>($"/api/devices/greenhouse/{greenhouseId}");
        devices.EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("status").GetString().Should().Be("active");
    }

    // Threshold 10–30, reading 41 → waits until the evaluation job (1 s in tests) has raised the alert.
    private async Task RaiseAlertAsync(HttpClient client, Guid greenhouseId, string deviceToken)
    {
        (await client.PostAsJsonAsync("/api/thresholds",
            new { greenhouseId, metric = "Temperature", minValue = 10, maxValue = 30, isEnabled = true }))
            .EnsureSuccessStatusCode();
        (await IngestAsync(deviceToken, "Temperature", 41, DateTimeOffset.UtcNow.ToString("O"))).EnsureSuccessStatusCode();

        for (var i = 0; i < 40; i++)
        {
            var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
            if (alerts.GetProperty("total").GetInt32() > 0) return;
            await Task.Delay(500);
        }
        throw new TimeoutException("No alert was raised");
    }

    private async Task<(HttpClient Client, string Token)> RegisterAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            organizationName = "Org " + Guid.NewGuid().ToString("N")[..6],
            email = $"{Guid.NewGuid():N}@it.test",
            password = "Passw0rd!123",
        });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private static async Task<Guid> CreateGreenhouseAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/greenhouses", new { name = "GH", location = "Antalya" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string Token)> CreateDeviceAsync(HttpClient client, Guid greenhouseId, string metric)
    {
        var response = await client.PostAsJsonAsync("/api/devices", new { name = metric, greenhouseId, metric });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), body.GetProperty("token").GetString()!);
    }

    private Task<HttpResponseMessage> IngestAsync(string deviceToken, string metric, double value, string recordedAt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest")
        {
            // Raw JSON so the timestamp reaches the API exactly as a device would send it.
            Content = new StringContent(
                $$"""{"metric":"{{metric}}","value":{{value}},"recordedAt":"{{recordedAt}}"}""",
                System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Device-Token", deviceToken);
        return factory.CreateClient().SendAsync(request);
    }

    private async Task<HubConnection> ConnectHubAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/telemetry"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.Transports = HttpTransportType.LongPolling; // TestServer has no real sockets
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.IntegrationTests;

/// <summary>Registering a board and its channels through the API, then reporting with its token.</summary>
[Collection(ApiCollection.Name)]
public sealed class NodeEndpointsTests(SensSeraApiFactory factory)
{
    [Fact]
    public async Task CreateNode_AddChannel_NodeTokenIngests_AndChannelShowsOnNode()
    {
        var client = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);

        var created = await client.PostAsJsonAsync("/api/nodes", new { name = "esp32-balkon", greenhouseId });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var node = await created.Content.ReadFromJsonAsync<JsonElement>();
        var nodeId = node.GetProperty("id").GetGuid();
        var nodeToken = node.GetProperty("token").GetString()!;
        nodeToken.Should().StartWith($"{nodeId}.");

        var channel = await client.PostAsJsonAsync($"/api/nodes/{nodeId}/channels",
            new { channel = "soil1", name = "Fesleğen toprak", metric = "SoilMoisture" });
        channel.StatusCode.Should().Be(HttpStatusCode.Created);
        var deviceId = (await channel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceId").GetGuid();

        (await IngestAsync(nodeToken, "soil1")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/nodes/{nodeId}");
        fetched.GetProperty("lastSeenAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        var listed = fetched.GetProperty("channels").EnumerateArray().Should().ContainSingle().Subject;
        listed.GetProperty("deviceId").GetGuid().Should().Be(deviceId);
        listed.GetProperty("channel").GetString().Should().Be("soil1");

        var device = await client.GetFromJsonAsync<JsonElement>($"/api/devices/{deviceId}");
        device.GetProperty("nodeId").GetGuid().Should().Be(nodeId);
        device.GetProperty("channel").GetString().Should().Be("soil1");
    }

    [Fact]
    public async Task RotateToken_OldTokenStopsWorking_NewTokenWorks()
    {
        var client = await RegisterAsync();
        var (nodeId, oldToken) = await CreateNodeWithChannelAsync(client, await CreateGreenhouseAsync(client));

        var rotated = await client.PostAsync($"/api/nodes/{nodeId}/rotate-token", null);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var newToken = (await rotated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        (await IngestAsync(oldToken, "soil1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await IngestAsync(newToken, "soil1")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddChannel_DuplicateName_Returns409()
    {
        var client = await RegisterAsync();
        var (nodeId, _) = await CreateNodeWithChannelAsync(client, await CreateGreenhouseAsync(client));

        var response = await client.PostAsJsonAsync($"/api/nodes/{nodeId}/channels",
            new { channel = "soil1", name = "again", metric = "SoilMoisture" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("Soil1")]
    [InlineData("soil 1")]
    [InlineData("")]
    public async Task AddChannel_InvalidChannelName_Returns400(string channel)
    {
        var client = await RegisterAsync();
        var (nodeId, _) = await CreateNodeWithChannelAsync(client, await CreateGreenhouseAsync(client));

        var response = await client.PostAsJsonAsync($"/api/nodes/{nodeId}/channels",
            new { channel, name = "x", metric = "SoilMoisture" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChannelDevice_CannotRotateOwnToken_OrMoveGreenhouse()
    {
        var client = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (nodeId, _) = await CreateNodeWithChannelAsync(client, greenhouseId);
        var deviceId = await ChannelDeviceIdAsync(client, nodeId);

        (await client.PostAsync($"/api/devices/{deviceId}/rotate-token", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var otherGreenhouseId = await CreateGreenhouseAsync(client);
        (await client.PutAsJsonAsync($"/api/devices/{deviceId}",
                new { name = "moved", greenhouseId = otherGreenhouseId, metric = "SoilMoisture" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteNode_RemovesChannelsWithTheirReadingsAndAlerts()
    {
        var client = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(client);
        var (nodeId, nodeToken) = await CreateNodeWithChannelAsync(client, greenhouseId);
        var deviceId = await ChannelDeviceIdAsync(client, nodeId);
        (await IngestAsync(nodeToken, "soil1")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await SeedAlertAsync(client, greenhouseId, deviceId);

        (await client.DeleteAsync($"/api/nodes/{nodeId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/nodes/{nodeId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using var db = CreateDb();
        (await db.Devices.IgnoreQueryFilters().AnyAsync(d => d.Id == deviceId)).Should().BeFalse();
        (await db.SensorReadings.IgnoreQueryFilters().AnyAsync(r => r.DeviceId == deviceId)).Should().BeFalse();
        (await db.Alerts.IgnoreQueryFilters().AnyAsync(a => a.DeviceId == deviceId)).Should().BeFalse();
        (await IngestAsync(nodeToken, "soil1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtherOrganization_GetsNotFound_OnEveryNodeEndpoint()
    {
        var owner = await RegisterAsync();
        var greenhouseId = await CreateGreenhouseAsync(owner);
        var (nodeId, _) = await CreateNodeWithChannelAsync(owner, greenhouseId);
        var intruder = await RegisterAsync();

        (await intruder.GetAsync($"/api/nodes/{nodeId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.PostAsync($"/api/nodes/{nodeId}/rotate-token", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.PostAsJsonAsync($"/api/nodes/{nodeId}/channels",
            new { channel = "soil2", name = "x", metric = "SoilMoisture" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.DeleteAsync($"/api/nodes/{nodeId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.PostAsJsonAsync("/api/nodes", new { name = "x", greenhouseId }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.GetFromJsonAsync<JsonElement>($"/api/nodes/greenhouse/{greenhouseId}"))
            .GetArrayLength().Should().Be(0);

        (await owner.GetAsync($"/api/nodes/{nodeId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpClient> RegisterAsync()
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
        return client;
    }

    private static async Task<Guid> CreateGreenhouseAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/greenhouses", new { name = "GH", location = "İzmir" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid NodeId, string Token)> CreateNodeWithChannelAsync(HttpClient client, Guid greenhouseId)
    {
        var created = await client.PostAsJsonAsync("/api/nodes", new { name = "esp32", greenhouseId });
        created.EnsureSuccessStatusCode();
        var node = await created.Content.ReadFromJsonAsync<JsonElement>();
        var nodeId = node.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/nodes/{nodeId}/channels",
            new { channel = "soil1", name = "Soil 1", metric = "SoilMoisture" })).EnsureSuccessStatusCode();
        return (nodeId, node.GetProperty("token").GetString()!);
    }

    private static async Task<Guid> ChannelDeviceIdAsync(HttpClient client, Guid nodeId)
    {
        var node = await client.GetFromJsonAsync<JsonElement>($"/api/nodes/{nodeId}");
        return node.GetProperty("channels")[0].GetProperty("deviceId").GetGuid();
    }

    // Raising a real alert waits on the evaluation job; the delete path only needs the row to exist.
    private async Task SeedAlertAsync(HttpClient client, Guid greenhouseId, Guid deviceId)
    {
        var threshold = await client.PostAsJsonAsync("/api/thresholds",
            new { greenhouseId, metric = "SoilMoisture", minValue = 30, maxValue = 70, isEnabled = true });
        threshold.EnsureSuccessStatusCode();
        var thresholdId = (await threshold.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var db = CreateDb();
        var device = await db.Devices.IgnoreQueryFilters().SingleAsync(d => d.Id == deviceId);
        db.Alerts.Add(new Alert
        {
            OrganizationId = device.OrganizationId, GreenhouseId = greenhouseId, DeviceId = deviceId,
            ThresholdId = thresholdId, Metric = MetricType.SoilMoisture, TriggeredValue = 10,
            Severity = AlertSeverity.Warning, Status = AlertStatus.Open, TriggeredAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> IngestAsync(string nodeToken, string channel)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest/batch")
        {
            Content = JsonContent.Create(new
            {
                readings = new[] { new { metric = "SoilMoisture", value = 42.0, recordedAt = DateTime.UtcNow, channel } },
            }),
        };
        request.Headers.Add("X-Device-Token", nodeToken);
        return factory.CreateClient().SendAsync(request);
    }

    private AppDbContext CreateDb() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
}

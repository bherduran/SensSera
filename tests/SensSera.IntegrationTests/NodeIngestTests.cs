using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.IntegrationTests;

/// <summary>
/// A node (board) authenticates once and tags each reading with a channel; the API maps the
/// channel to the node's device. Nodes are seeded directly until the node endpoints exist.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class NodeIngestTests(SensSeraApiFactory factory)
{
    private const string Secret = "node-secret-for-tests";

    [Fact]
    public async Task NodeToken_BatchAcrossChannels_StoresEachReadingOnItsDevice()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync(seed.NodeToken,
            new { metric = "SoilMoisture", value = 41.5, recordedAt = DateTime.UtcNow, channel = "soil1" },
            new { metric = "Temperature", value = 22.0, recordedAt = DateTime.UtcNow, channel = "air_temp" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = CreateDb();
        var readings = await db.SensorReadings.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == seed.OrgId).ToListAsync();
        readings.Should().HaveCount(2);
        readings.Single(r => r.DeviceId == seed.SoilDeviceId).Value.Should().Be(41.5);
        readings.Single(r => r.DeviceId == seed.AirDeviceId).Metric.Should().Be(MetricType.Temperature);
        (await db.Nodes.IgnoreQueryFilters().SingleAsync(n => n.Id == seed.NodeId)).LastSeenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NodeToken_UnknownChannel_Returns400_AndWritesNothing()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync(seed.NodeToken,
            new { metric = "SoilMoisture", value = 40.0, recordedAt = DateTime.UtcNow, channel = "soil1" },
            new { metric = "SoilMoisture", value = 40.0, recordedAt = DateTime.UtcNow, channel = "soil9" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("detail").GetString().Should().Be("Reading 1: unknown channel 'soil9'.");
        await AssertNoReadingsAsync(seed.OrgId);
    }

    [Fact]
    public async Task NodeToken_MetricNotMatchingChannel_Returns400()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync(seed.NodeToken,
            new { metric = "Temperature", value = 20.0, recordedAt = DateTime.UtcNow, channel = "soil1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoReadingsAsync(seed.OrgId);
    }

    [Fact]
    public async Task NodeToken_ReadingWithoutChannel_Returns400()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync(seed.NodeToken,
            new { metric = "SoilMoisture", value = 40.0, recordedAt = DateTime.UtcNow });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeviceToken_ReadingWithChannel_Returns400()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync(seed.StandaloneToken,
            new { metric = "Light", value = 500.0, recordedAt = DateTime.UtcNow, channel = "soil1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NodeToken_WrongSecret_Returns401()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync($"{seed.NodeId}.wrong-secret",
            new { metric = "SoilMoisture", value = 40.0, recordedAt = DateTime.UtcNow, channel = "soil1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The channel device is seeded with a hash of the same secret on purpose: even then its own
    // id must not work as a token, because channel devices only report through their node.
    [Fact]
    public async Task ChannelDevice_CannotAuthenticateWithItsOwnId()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync($"{seed.SoilDeviceId}.{Secret}",
            new { metric = "SoilMoisture", value = 40.0, recordedAt = DateTime.UtcNow });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NodeToken_WithFirmwareHeader_StoresVersionOnNode()
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync(seed.NodeToken, "0.1.0+b8bd68c8", SoilReading());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NodeFirmwareAsync(seed.NodeId)).Should().Be("0.1.0+b8bd68c8");
    }

    [Fact]
    public async Task NodeToken_WithoutFirmwareHeader_KeepsStoredVersion()
    {
        var seed = await SeedAsync();
        await PostBatchAsync(seed.NodeToken, "0.1.0+b8bd68c8", SoilReading());

        var response = await PostBatchAsync(seed.NodeToken, SoilReading());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NodeFirmwareAsync(seed.NodeId)).Should().Be("0.1.0+b8bd68c8");
    }

    // A bad label must not cost the readings: the request succeeds and the version is ignored.
    [Theory]
    [InlineData("0.1.0 <script>")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public async Task NodeToken_MalformedFirmwareHeader_StoresReadingsButNotVersion(string version)
    {
        var seed = await SeedAsync();

        var response = await PostBatchAsync(seed.NodeToken, version, SoilReading());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NodeFirmwareAsync(seed.NodeId)).Should().BeNull();
        await using var db = CreateDb();
        (await db.SensorReadings.IgnoreQueryFilters().CountAsync(r => r.OrganizationId == seed.OrgId)).Should().Be(1);
    }

    private static object SoilReading() =>
        new { metric = "SoilMoisture", value = 40.0, recordedAt = DateTime.UtcNow, channel = "soil1" };

    private async Task<string?> NodeFirmwareAsync(Guid nodeId)
    {
        await using var db = CreateDb();
        return (await db.Nodes.IgnoreQueryFilters().AsNoTracking().SingleAsync(n => n.Id == nodeId)).FirmwareVersion;
    }

    private sealed record Seed(Guid OrgId, Guid NodeId, Guid SoilDeviceId, Guid AirDeviceId, string NodeToken, string StandaloneToken);

    private async Task<Seed> SeedAsync()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Secret)));
        var org = new Organization { Name = "Node Ingest Org", Slug = $"ingest-{Guid.NewGuid():N}" };
        var greenhouse = new Greenhouse { OrganizationId = org.Id, Name = "GH" };
        var node = new Node { OrganizationId = org.Id, GreenhouseId = greenhouse.Id, Name = "esp32-1", TokenHash = hash };
        Device Channel(string channel, MetricType metric) => new()
        {
            OrganizationId = org.Id, GreenhouseId = greenhouse.Id, Name = channel, Metric = metric,
            DeviceTokenHash = hash, NodeId = node.Id, Channel = channel,
        };
        var soil = Channel("soil1", MetricType.SoilMoisture);
        var air = Channel("air_temp", MetricType.Temperature);
        var standalone = new Device
        {
            OrganizationId = org.Id, GreenhouseId = greenhouse.Id, Name = "light", Metric = MetricType.Light, DeviceTokenHash = hash,
        };

        await using var db = CreateDb();
        db.AddRange(org, greenhouse, node, soil, air, standalone);
        await db.SaveChangesAsync();
        return new Seed(org.Id, node.Id, soil.Id, air.Id, $"{node.Id}.{Secret}", $"{standalone.Id}.{Secret}");
    }

    private Task<HttpResponseMessage> PostBatchAsync(string token, params object[] readings) =>
        PostBatchAsync(token, firmwareVersion: null, readings);

    private Task<HttpResponseMessage> PostBatchAsync(string token, string? firmwareVersion, params object[] readings)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest/batch")
        {
            Content = JsonContent.Create(new { readings }),
        };
        request.Headers.Add("X-Device-Token", token);
        if (firmwareVersion is not null)
            request.Headers.TryAddWithoutValidation("X-Firmware-Version", firmwareVersion);
        return factory.CreateClient().SendAsync(request);
    }

    private async Task AssertNoReadingsAsync(Guid orgId)
    {
        await using var db = CreateDb();
        (await db.SensorReadings.IgnoreQueryFilters().CountAsync(r => r.OrganizationId == orgId)).Should().Be(0);
    }

    private AppDbContext CreateDb() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
}

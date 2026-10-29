using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.IntegrationTests;

/// <summary>
/// The node/channel invariants live in Postgres (FK actions, unique index, check constraint),
/// so they are asserted against the real database rather than through EF InMemory.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class NodeSchemaTests(SensSeraApiFactory factory)
{
    [Fact]
    public async Task DeletingGreenhouse_RemovesItsNodesAndChannelDevices()
    {
        var (greenhouseId, nodeId) = await SeedNodeAsync();
        await AddDeviceAsync(greenhouseId, nodeId, "soil1");

        await using (var db = CreateDb())
            await db.Greenhouses.IgnoreQueryFilters().Where(g => g.Id == greenhouseId).ExecuteDeleteAsync();

        await using var check = CreateDb();
        (await check.Nodes.IgnoreQueryFilters().AnyAsync(n => n.Id == nodeId)).Should().BeFalse();
        (await check.Devices.IgnoreQueryFilters().AnyAsync(d => d.NodeId == nodeId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingNode_WithChannelDevices_IsRejected()
    {
        var (greenhouseId, nodeId) = await SeedNodeAsync();
        await AddDeviceAsync(greenhouseId, nodeId, "soil1");

        await using var db = CreateDb();
        var act = () => db.Nodes.IgnoreQueryFilters().Where(n => n.Id == nodeId).ExecuteDeleteAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState
            .Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task SameChannelTwiceOnOneNode_IsRejected()
    {
        var (greenhouseId, nodeId) = await SeedNodeAsync();
        await AddDeviceAsync(greenhouseId, nodeId, "soil1");

        var act = () => AddDeviceAsync(greenhouseId, nodeId, "soil1");

        (await act.Should().ThrowAsync<DbUpdateException>()).WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Theory]
    [InlineData(true, null)]    // on a node but no channel name
    [InlineData(false, "soil1")] // channel name without a node
    public async Task NodeAndChannel_MustBeSetTogether(bool onNode, string? channel)
    {
        var (greenhouseId, nodeId) = await SeedNodeAsync();

        var act = () => AddDeviceAsync(greenhouseId, onNode ? nodeId : null, channel);

        (await act.Should().ThrowAsync<DbUpdateException>()).WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task StandaloneDevices_AreNotConstrainedByChannelIndex()
    {
        var (greenhouseId, _) = await SeedNodeAsync();

        await AddDeviceAsync(greenhouseId, null, null);
        var act = () => AddDeviceAsync(greenhouseId, null, null);

        await act.Should().NotThrowAsync();
    }

    private AppDbContext CreateDb() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private Guid _orgId;

    private async Task<(Guid GreenhouseId, Guid NodeId)> SeedNodeAsync()
    {
        var org = new Organization { Name = "Node Org", Slug = $"node-{Guid.NewGuid():N}" };
        var greenhouse = new Greenhouse { OrganizationId = org.Id, Name = "GH" };
        var node = new Node { OrganizationId = org.Id, GreenhouseId = greenhouse.Id, Name = "esp32-1", TokenHash = "hash" };
        _orgId = org.Id;

        await using var db = CreateDb();
        db.AddRange(org, greenhouse, node);
        await db.SaveChangesAsync();
        return (greenhouse.Id, node.Id);
    }

    private async Task AddDeviceAsync(Guid greenhouseId, Guid? nodeId, string? channel)
    {
        await using var db = CreateDb();
        db.Devices.Add(new Device
        {
            OrganizationId = _orgId,
            GreenhouseId = greenhouseId,
            Name = channel ?? "standalone",
            Metric = MetricType.SoilMoisture,
            DeviceTokenHash = "hash",
            NodeId = nodeId,
            Channel = channel,
        });
        await db.SaveChangesAsync();
    }
}

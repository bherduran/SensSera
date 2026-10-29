using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Exceptions;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class NodeService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : INodeService
{
    public async Task<List<NodeResponse>> GetAllByGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        return await db.Nodes
            .AsNoTracking()
            .Where(n => n.GreenhouseId == greenhouseId && n.OrganizationId == orgId)
            .OrderBy(n => n.Name)
            .Select(ToResponse)
            .ToListAsync(cancellationToken);
    }

    public async Task<NodeResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        return await db.Nodes
            .AsNoTracking()
            .Where(n => n.Id == id && n.OrganizationId == orgId)
            .Select(ToResponse)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Node {id} not found");
    }

    public async Task<NodeWithTokenResponse> CreateAsync(NodeRequest request, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var greenhouseExists = await db.Greenhouses
            .AnyAsync(g => g.Id == request.GreenhouseId && g.OrganizationId == orgId, cancellationToken);
        if (!greenhouseExists)
            throw new KeyNotFoundException($"Greenhouse {request.GreenhouseId} not found");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // The id is app-generated, so the token can be built before the first save.
        var node = new Node
        {
            OrganizationId = orgId,
            GreenhouseId = request.GreenhouseId,
            Name = request.Name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var (rawToken, tokenHash) = TokenGenerator.Create(node.Id);
        node.TokenHash = tokenHash;

        db.Nodes.Add(node);
        await db.SaveChangesAsync(cancellationToken);
        return new NodeWithTokenResponse(node.Id, node.Name, node.GreenhouseId, rawToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await FindNodeAsync(id, cancellationToken);

        // Same order as a device delete, for every channel at once: alerts (RESTRICT FK) first,
        // then the channel devices (their readings and rollups cascade), then the node, whose
        // FK from Device is NO ACTION. One transaction so a failure leaves no half-deleted board.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Alerts
            .Where(a => db.Devices.Any(d => d.Id == a.DeviceId && d.NodeId == id))
            .ExecuteDeleteAsync(cancellationToken);
        await db.Devices
            .Where(d => d.NodeId == id)
            .ExecuteDeleteAsync(cancellationToken);
        db.Nodes.Remove(node);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<NodeWithTokenResponse> RotateTokenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await FindNodeAsync(id, cancellationToken);

        var (rawToken, tokenHash) = TokenGenerator.Create(node.Id);
        node.TokenHash = tokenHash;
        node.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);
        return new NodeWithTokenResponse(node.Id, node.Name, node.GreenhouseId, rawToken);
    }

    public async Task<NodeChannelResponse> AddChannelAsync(Guid nodeId, NodeChannelRequest request, CancellationToken cancellationToken = default)
    {
        var node = await FindNodeAsync(nodeId, cancellationToken);

        if (await db.Devices.AnyAsync(d => d.NodeId == nodeId && d.Channel == request.Channel, cancellationToken))
            throw new ConflictException($"Node already has a channel named '{request.Channel}'.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var device = new Device
        {
            OrganizationId = node.OrganizationId,
            GreenhouseId = node.GreenhouseId,
            Name = request.Name,
            Metric = request.Metric,
            Status = DeviceStatus.Active,
            NodeId = node.Id,
            Channel = request.Channel,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // Channel devices report through their node and the auth handler refuses them a token of
        // their own. The column is still required, so it gets the hash of a secret nobody receives.
        device.DeviceTokenHash = TokenGenerator.Create(device.Id).TokenHash;

        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
        return new NodeChannelResponse(device.Id, request.Channel, device.Name, device.Metric, device.LastSeenAt);
    }

    private async Task<Node> FindNodeAsync(Guid id, CancellationToken cancellationToken)
    {
        var orgId = tenant.RequireOrganizationId();
        return await db.Nodes
            .Where(n => n.Id == id && n.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Node {id} not found");
    }

    private static readonly System.Linq.Expressions.Expression<Func<Node, NodeResponse>> ToResponse = n =>
        new NodeResponse(
            n.Id,
            n.Name,
            n.GreenhouseId,
            n.FirmwareVersion,
            n.LastSeenAt,
            n.CreatedAt,
            n.Devices
                .OrderBy(d => d.Channel)
                .Select(d => new NodeChannelResponse(d.Id, d.Channel!, d.Name, d.Metric, d.LastSeenAt))
                .ToList());
}

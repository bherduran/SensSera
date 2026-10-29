using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class DeviceService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IDeviceService
{
    
    public async Task<List<DeviceResponse>> GetAllByGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        return await db.Devices
            .AsNoTracking()
            .Where(d => d.GreenhouseId == greenhouseId && d.OrganizationId == orgId)
            .Select(d => ToResponse(d))
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var d = await db.Devices
            .AsNoTracking()
            .Where(d => d.Id == id && d.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Device {id} not found");

        return ToResponse(d);
    }
    public async Task<DeviceWithTokenResponse> CreateAsync(DeviceRequest request, CancellationToken cancellationToken = default)
    {
        await VerifyGreenhouseOwnershipAsync(request.GreenhouseId, cancellationToken);

        var orgId = tenant.RequireOrganizationId();
        var device = new Device
        {
            Name = request.Name,
            GreenhouseId = request.GreenhouseId,
            OrganizationId = orgId,
            Metric = request.Metric,
            Status = DeviceStatus.Active,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);

        var (rawToken, tokenHash) = TokenGenerator.Create(device.Id);
        device.DeviceTokenHash = tokenHash;
        await db.SaveChangesAsync(cancellationToken);

        return new DeviceWithTokenResponse(device.Id, device.Name, device.GreenhouseId, device.Metric, rawToken);
    }


    public async Task<DeviceResponse> UpdateAsync(Guid id, DeviceRequest request, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var d = await db.Devices
            .Where(d => d.Id == id && d.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Device {id} not found");

        await VerifyGreenhouseOwnershipAsync(request.GreenhouseId, cancellationToken);

        // A channel lives on a board, and the board sits in one greenhouse.
        if (d.NodeId is not null && request.GreenhouseId != d.GreenhouseId)
            throw new ArgumentException("A node channel cannot move to another greenhouse; move or recreate the node instead.");

        d.Name = request.Name;
        d.GreenhouseId = request.GreenhouseId;
        d.Metric = request.Metric;
        d.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(d);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var d = await db.Devices
            .Where(d => d.Id == id && d.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Device {id} not found");

        // Alerts reference the device via a RESTRICT FK, so the device's alert history goes first;
        // readings and rollups cascade.
        await db.Alerts
            .Where(a => a.DeviceId == id)
            .ExecuteDeleteAsync(cancellationToken);

        db.Devices.Remove(d);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeviceWithTokenResponse> RotateTokenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var d = await db.Devices
            .Where(d => d.Id == id && d.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Device {id} not found");

        // A channel device's own token is never accepted, so handing one out would only mislead.
        if (d.NodeId is not null)
            throw new ArgumentException("This device is a node channel; rotate the node's token instead.");

        var (rawToken, tokenHash) = TokenGenerator.Create(d.Id);
        d.DeviceTokenHash = tokenHash;
        d.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);
        return new DeviceWithTokenResponse(d.Id, d.Name, d.GreenhouseId, d.Metric, rawToken);
    }

    private async Task VerifyGreenhouseOwnershipAsync(Guid greenhouseId, CancellationToken cancellationToken)
    {
        var orgId = tenant.RequireOrganizationId();
        var exists = await db.Greenhouses
            .AnyAsync(g => g.Id == greenhouseId && g.OrganizationId == orgId, cancellationToken);

        if (!exists)
            throw new KeyNotFoundException($"Greenhouse {greenhouseId} not found");
    }
    
    private static DeviceResponse ToResponse(Device d) =>
        new(d.Id, d.Name, d.GreenhouseId, d.Metric, d.Status, d.CreatedAt, d.NodeId, d.Channel);
}
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public class DeviceService(AppDbContext db, ITenantContext tenant) : IDeviceService
{
    
    public async Task<List<DeviceResponse>> GetAllByGreenhouseAsync(Guid greenhouseId)
    {
        return await db.Devices
            .Where(d => d.GreenhouseId == greenhouseId && d.OrganizationId == tenant.OrganizationId)
            .Select(d => ToResponse(d))
            .ToListAsync();
    }

    public async Task<DeviceResponse> GetByIdAsync(Guid id)
    {
        var d = await db.Devices
            .Where(d => d.Id == id && d.OrganizationId == tenant.OrganizationId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Device {id} not found");

        return ToResponse(d);    
    }
    public async Task<DeviceWithTokenResponse> CreateAsync(DeviceRequest request)
    {
        await VerifyGreenhouseOwnershipAsync(request.GreenhouseId);

        var device = new Device
        {
            Name = request.Name,
            GreenhouseId = request.GreenhouseId,
            OrganizationId = tenant.OrganizationId,
            Status = DeviceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var (rawToken, tokenHash) = GenerateToken(device.Id);
        device.DeviceTokenHash = tokenHash;
        await db.SaveChangesAsync();

        return new DeviceWithTokenResponse(device.Id, device.Name, device.GreenhouseId, rawToken);
    }


    public async Task<DeviceResponse> UpdateAsync(Guid id, DeviceRequest request)
    {
        var d = await db.Devices
            .Where(d => d.Id == id && d.OrganizationId == tenant.OrganizationId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Device {id} not found");

        await VerifyGreenhouseOwnershipAsync(request.GreenhouseId);

        d.Name = request.Name;
        d.GreenhouseId = request.GreenhouseId;
        d.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToResponse(d);    
    }

    public async Task DeleteAsync(Guid id)
    {
        var d = await db.Devices
            .Where(d => d.Id == id && d.OrganizationId == tenant.OrganizationId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Device {id} not found");

        db.Devices.Remove(d);
        await db.SaveChangesAsync();    
    }

    public async Task<DeviceWithTokenResponse> RotateTokenAsync(Guid id)
    {
        var d = await db.Devices
            .Where(d => d.Id == id && d.OrganizationId == tenant.OrganizationId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Device {id} not found");

        var (rawToken, tokenHash) = GenerateToken(d.Id);
        d.DeviceTokenHash = tokenHash;
        d.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return new DeviceWithTokenResponse(d.Id, d.Name, d.GreenhouseId, rawToken);    
    }

    private async Task VerifyGreenhouseOwnershipAsync(Guid greenhouseId)
    {
        var exists = await db.Greenhouses
            .AnyAsync(g => g.Id == greenhouseId && g.OrganizationId == tenant.OrganizationId);

        if (!exists)
            throw new KeyNotFoundException($"Greenhouse {greenhouseId} not found");    
    }
    
    private static (string rawToken, string tokenHash) GenerateToken(Guid deviceId)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var raw = $"{deviceId}.{secret}";
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));
        return (raw, hash);
    }

    private static DeviceResponse ToResponse(Device d) =>
        new(d.Id, d.Name, d.GreenhouseId, d.Status.ToString(), d.CreatedAt);
}
using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public class GreenhouseService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IGreenhouseService
{
    public async Task<List<GreenhouseResponse>> GetAllAsync()
    {
        var orgId = tenant.RequireOrganizationId();
        return await db.Greenhouses
            .Where(g => g.OrganizationId == orgId)
            .Select(g => ToResponse(g))
            .ToListAsync();
    }

    public async Task<GreenhouseResponse> GetByIdAsync(Guid id)
    {
        var orgId = tenant.RequireOrganizationId();
        var g = await db.Greenhouses
            .Where(g => g.Id == id && g.OrganizationId == orgId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Greenhouse {id} not found");

        return ToResponse(g);    
    }

    public async Task<GreenhouseResponse> CreateAsync(GreenhouseRequest request)
    {
        var orgId = tenant.RequireOrganizationId();
        var g = new Greenhouse
        {
            Name = request.Name,
            Location = request.Location,
            OrganizationId = orgId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        db.Greenhouses.Add(g);
        await db.SaveChangesAsync();
        return ToResponse(g);
    }

    public async Task<GreenhouseResponse> UpdateAsync(Guid id, GreenhouseRequest request)
    {
        var orgId = tenant.RequireOrganizationId();
        var g = await db.Greenhouses
            .Where(g => g.Id == id && g.OrganizationId == orgId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Greenhouse {id} not found");

        g.Name = request.Name;
        g.Location = request.Location;
        g.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync();
        return ToResponse(g);    
    }

    public async Task DeleteAsync(Guid id)
    {
        var orgId = tenant.RequireOrganizationId();
        var g = await db.Greenhouses
            .Where(g => g.Id == id && g.OrganizationId == orgId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Greenhouse {id} not found");


        db.Greenhouses.Remove(g);
        await db.SaveChangesAsync();
        
    }

    private static GreenhouseResponse ToResponse(Greenhouse g) =>
        new(g.Id, g.Name, g.Location, g.CreatedAt);
}


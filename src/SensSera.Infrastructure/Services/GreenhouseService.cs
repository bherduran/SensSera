using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public class GreenhouseService(AppDbContext db, ITenantContext tenant) : IGreenhouseService
{
    public async Task<List<GreenhouseResponse>> GetAllAsync()
    {
        return await db.Greenhouses
            .Where(g => g.OrganizationId == tenant.OrganizationId)
            .Select(g => ToResponse(g))
            .ToListAsync();
    }

    public async Task<GreenhouseResponse> GetByIdAsync(Guid id)
    {
        var g = await db.Greenhouses
            .Where(g => g.Id == id && g.OrganizationId == tenant.OrganizationId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Greenhouse {id} not found");

        return ToResponse(g);    
    }

    public async Task<GreenhouseResponse> CreateAsync(GreenhouseRequest request)
    {
        var g = new Greenhouse
        {
            Name = request.Name,
            Location = request.Location,
            OrganizationId = tenant.OrganizationId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Greenhouses.Add(g);
        await db.SaveChangesAsync();
        return ToResponse(g);
    }

    public async Task<GreenhouseResponse> UpdateAsync(Guid id, GreenhouseRequest request)
    {
        var g = await db.Greenhouses
            .Where(g => g.Id == id && g.OrganizationId == tenant.OrganizationId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Greenhouse {id} not found");

        g.Name = request.Name;
        g.Location = request.Location;
        g.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToResponse(g);    
    }

    public async Task DeleteAsync(Guid id)
    {
        var g = await db.Greenhouses
            .Where(g => g.Id == id && g.OrganizationId == tenant.OrganizationId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Greenhouse {id} not found");


        db.Greenhouses.Remove(g);
        await db.SaveChangesAsync();
        
    }

    private static GreenhouseResponse ToResponse(Greenhouse g) =>
        new(g.Id, g.Name, g.Location, g.CreatedAt);
}


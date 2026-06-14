using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public class ThresholdService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IThresholdService
{
    public async Task<List<ThresholdResponse>> GetAllByGreenhouseAsync(Guid greenhouseId)
    {
        var orgId = tenant.RequireOrganizationId();
        return await db.Thresholds
            .Where(t=> t.GreenhouseId == greenhouseId && t.OrganizationId == orgId)
            .Select(t => ToResponse(t))
            .ToListAsync();
    }

    public async Task<ThresholdResponse> GetByIdAsync(Guid id)
    {
        var orgId = tenant.RequireOrganizationId();
        var t = await db.Thresholds
            .Where(t => t.Id == id && t.OrganizationId == orgId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Threshold {id} not found");
        return ToResponse(t);    
    }

    public async Task<ThresholdResponse> CreateAsync(ThresholdRequest request)
    {
        var orgId = tenant.RequireOrganizationId();
        await VerifyGreenhouseOwnershipAsync(request.GreenhouseId, orgId);

        if (!Enum.TryParse<MetricType>(request.Metric, ignoreCase: true, out var metric))
            throw new ArgumentException($"Unknown metric: {request.Metric}");

        if (request.MinValue is null && request.MaxValue is null)
            throw new ArgumentException("At least one of MinValue or MaxValue must be set");

        var t = new Threshold
        {
            OrganizationId = orgId,
            GreenhouseId = request.GreenhouseId,
            Metric = metric,
            MinValue = request.MinValue,
            MaxValue = request.MaxValue,
            IsEnabled = request.IsEnabled,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        db.Thresholds.Add(t);
        await db.SaveChangesAsync();
        return ToResponse(t);
    }

    public async Task<ThresholdResponse> UpdateAsync(Guid id, ThresholdRequest request)
    {
        var orgId = tenant.RequireOrganizationId();
        var t = await db.Thresholds
            .Where(t => t.Id == id && t.OrganizationId == orgId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Threshold {id} not found");

        if (!Enum.TryParse<MetricType>(request.Metric, ignoreCase: true, out var metric))
            throw new ArgumentException($"Unknown metric: {request.Metric}");

        if (request.MinValue is null && request.MaxValue is null)
            throw new ArgumentException("At least one of MinValue or MaxValue must be set");

        t.Metric = metric;
        t.MinValue = request.MinValue;            
        t.MaxValue = request.MaxValue; 
        t.IsEnabled = request.IsEnabled;
        t.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync();
        return ToResponse(t);           
    }

    public async Task DeleteAsync(Guid id)
    {
        var orgId = tenant.RequireOrganizationId();
        var t = await db.Thresholds
            .Where(t => t.Id == id && t.OrganizationId == orgId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Threshold {id} not found");

        db.Thresholds.Remove(t);
        await db.SaveChangesAsync();
    }

    private async Task VerifyGreenhouseOwnershipAsync(Guid greenhouseId, Guid orgId)
    {
        var exist = await db.Greenhouses
            .AnyAsync(g => g.Id == greenhouseId && g.OrganizationId == orgId);
        if (!exist)
            throw new KeyNotFoundException($"Greenhouse {greenhouseId} not found");
    }

    private static ThresholdResponse ToResponse(Threshold t) =>
        new(t.Id, t.GreenhouseId, t.Metric.ToString(), t.MinValue, t.MaxValue, t.IsEnabled, t.CreatedAt);

}
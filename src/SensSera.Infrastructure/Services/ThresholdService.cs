using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class ThresholdService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IThresholdService
{
    public async Task<List<ThresholdResponse>> GetAllByGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        return await db.Thresholds
            .AsNoTracking()
            .Where(t=> t.GreenhouseId == greenhouseId && t.OrganizationId == orgId)
            .Select(t => ToResponse(t))
            .ToListAsync(cancellationToken);
    }

    public async Task<ThresholdResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var t = await db.Thresholds
            .AsNoTracking()
            .Where(t => t.Id == id && t.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Threshold {id} not found");
        return ToResponse(t);    
    }

    public async Task<ThresholdResponse> CreateAsync(ThresholdRequest request, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        await VerifyGreenhouseOwnershipAsync(request.GreenhouseId, orgId, cancellationToken);

        if (request.MinValue is null && request.MaxValue is null)
            throw new ArgumentException("At least one of MinValue or MaxValue must be set");

        var t = new Threshold
        {
            OrganizationId = orgId,
            GreenhouseId = request.GreenhouseId,
            Metric = request.Metric,
            MinValue = request.MinValue,
            MaxValue = request.MaxValue,
            IsEnabled = request.IsEnabled,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        db.Thresholds.Add(t);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(t);
    }

    public async Task<ThresholdResponse> UpdateAsync(Guid id, ThresholdRequest request, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var t = await db.Thresholds
            .Where(t => t.Id == id && t.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Threshold {id} not found");

        if (request.MinValue is null && request.MaxValue is null)
            throw new ArgumentException("At least one of MinValue or MaxValue must be set");

        t.Metric = request.Metric;
        t.MinValue = request.MinValue;            
        t.MaxValue = request.MaxValue; 
        t.IsEnabled = request.IsEnabled;
        t.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(t);           
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();
        var t = await db.Thresholds
            .Where(t => t.Id == id && t.OrganizationId == orgId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Threshold {id} not found");

        // Alerts reference the threshold via a RESTRICT FK, so remove the
        // threshold's alert history first; otherwise the delete is blocked.
        await db.Alerts
            .Where(a => a.ThresholdId == id)
            .ExecuteDeleteAsync(cancellationToken);

        db.Thresholds.Remove(t);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task VerifyGreenhouseOwnershipAsync(Guid greenhouseId, Guid orgId, CancellationToken cancellationToken = default)
    {
        var exist = await db.Greenhouses
            .AnyAsync(g => g.Id == greenhouseId && g.OrganizationId == orgId, cancellationToken);
        if (!exist)
            throw new KeyNotFoundException($"Greenhouse {greenhouseId} not found");
    }

    private static ThresholdResponse ToResponse(Threshold t) =>
        new(t.Id, t.GreenhouseId, t.Metric, t.MinValue, t.MaxValue, t.IsEnabled, t.CreatedAt);

}
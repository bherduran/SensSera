using Microsoft.EntityFrameworkCore;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

public sealed class AlertService(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IAlertService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<AlertResponse>> ListAsync (AlertQuery query, CancellationToken cancellationToken = default)
    {
        _ = tenant.RequireOrganizationId();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var q = db.Alerts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<AlertStatus>(query.Status, ignoreCase: true, out var status))
            q = q.Where(a => a.Status == status);

        if (query.GreenhouseId is {} gid)
            q = q.Where(a => a.GreenhouseId == gid);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(a => a.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => ToResponse(a))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AlertResponse>(items, total, page, pageSize);
    }

    public async Task<AlertResponse> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = tenant.RequireOrganizationId();
        var a = await db.Alerts
            .Where(a => a.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Alert {id} not found");

        if (a.Status == AlertStatus.Open)
            a.Status = AlertStatus.Acknowledged;

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(a);        
    }

    public async Task<AlertResponse> ResolveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = tenant.RequireOrganizationId();
        var a = await db.Alerts
            .Where(a => a.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Alert {id} not found");

        a.Status = AlertStatus.Resolved;
        a.ResolvedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(a);  
    }

    private static AlertResponse ToResponse(Alert a) =>
        new(a.Id, a.GreenhouseId, a.DeviceId, a.ThresholdId,
        a.Metric, a.TriggeredValue, a.Severity, a.Status,
        a.TriggeredAt, a.ResolvedAt);
}
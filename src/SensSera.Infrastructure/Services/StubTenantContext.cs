using SensSera.Application.Interfaces;
using SensSera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SensSera.Infrastructure.Services;

public class StubTenantContext(AppDbContext db) : ITenantContext
{
    private Guid? _orgId;

    public Guid OrganizationId
    {
        get
        {
            if (_orgId is null)
                _orgId = db.Organizations.AsNoTracking()
                    .Where(o => o.Slug == "demo")
                    .Select(o => o.Id)
                    .First();
            return _orgId.Value;        
        }
    }
}
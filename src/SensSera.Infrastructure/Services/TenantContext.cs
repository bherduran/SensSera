using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SensSera.Application.Interfaces;

namespace SensSera.Infrastructure.Services;

public class TenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? OrganizationId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirst("org_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
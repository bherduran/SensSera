namespace SensSera.Application.Interfaces;

public interface ITenantContext
{
    Guid? OrganizationId { get; }
    bool HasTenant => OrganizationId is not null;
    Guid RequireOrganizationId() => OrganizationId
        ?? throw new InvalidOperationException("Tenant context required but not present");
}

namespace Gma.Framework.Tenancy;

public interface ITenantContextAccessor : ITenantContext
{
    void SetTenant(string tenantId);
    void ClearTenant();
}

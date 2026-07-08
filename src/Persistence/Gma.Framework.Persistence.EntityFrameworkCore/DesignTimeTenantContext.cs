namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Gma.Framework.Tenancy;

public sealed class DesignTimeTenantContext : ITenantContext
{
    public bool IsEnabled => false;
    public string? TenantId => "default";
}

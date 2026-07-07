namespace Gma.Framework.Tenancy.Infrastructure;

using Gma.Framework.Naming;
using Microsoft.Extensions.Options;
using Gma.Framework.Tenancy;

internal sealed class NullTenantContext(IOptions<TenantOptions> options) : ITenantContextAccessor
{
    private string? tenantId = NormalizeDefaultTenantId(options.Value);

    public bool IsEnabled => options.Value.Enabled;
    public string? TenantId => this.tenantId;

    public void SetTenant(string tenantId) => this.tenantId = TenantIds.Normalize(tenantId);

    public void ClearTenant() => this.tenantId = NormalizeDefaultTenantId(options.Value);

    private static string NormalizeDefaultTenantId(TenantOptions options) =>
        TenantIds.Normalize(options.LocalDefaultTenantId);
}

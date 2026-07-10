namespace Gma.Framework.Tenancy.Scoping;

using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Gma.Framework.Tenancy;
using Microsoft.Extensions.Options;

internal sealed class TenantScopeContext(
    ITenantContextAccessor tenantContext,
    IOptions<ScopeOptions> scopeOptions)
    : IScopeContextAccessor
{
    private string? localScopeId = NormalizeDefaultScopeId(scopeOptions.Value);

    public bool IsEnabled => tenantContext.IsEnabled || scopeOptions.Value.Enabled;

    public string? ScopeId => tenantContext.IsEnabled
        ? tenantContext.TenantId
        : this.localScopeId;

    public void SetScope(string scopeId)
    {
        string normalized = ScopeIds.Normalize(scopeId);
        if (tenantContext.IsEnabled)
        {
            tenantContext.SetTenant(normalized);
            return;
        }

        this.localScopeId = normalized;
    }

    public void ClearScope()
    {
        if (tenantContext.IsEnabled)
        {
            tenantContext.ClearTenant();
            return;
        }

        this.localScopeId = NormalizeDefaultScopeId(scopeOptions.Value);
    }

    private static string NormalizeDefaultScopeId(ScopeOptions options) =>
        ScopeIds.Normalize(options.LocalDefaultScopeId);
}

namespace Gma.Framework.Tenancy.Cqrs;

using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Observability;
using Gma.Framework.Tenancy;

internal sealed class TenantCqrsLogScopeContributor(ITenantContext tenantContext) : ICqrsLogScopeContributor
{
    public void Enrich(CqrsLogScopeContext context, IDictionary<string, object?> scopeProperties)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scopeProperties);

        scopeProperties[ObservabilityLogPropertyNames.TenantScoped] =
            !string.IsNullOrWhiteSpace(tenantContext.TenantId);
    }
}

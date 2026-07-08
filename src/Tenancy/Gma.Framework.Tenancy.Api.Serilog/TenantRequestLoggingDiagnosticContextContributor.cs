namespace Gma.Framework.Tenancy.Api.Serilog;

using Microsoft.AspNetCore.Http;
using global::Serilog;
using Gma.Framework.Api.Serilog;
using Gma.Framework.Observability;
using Gma.Framework.Tenancy;

internal sealed class TenantRequestLoggingDiagnosticContextContributor(ITenantContext tenantContext)
    : IRequestLoggingDiagnosticContextContributor
{
    public void Enrich(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(diagnosticContext);
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            diagnosticContext.Set(ObservabilityLogPropertyNames.TenantId, tenantContext.TenantId);
        }
    }
}

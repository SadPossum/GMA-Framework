namespace Gma.Framework.Api.Serilog;

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using global::Serilog;
using Gma.Framework.Api.Observability;
using Gma.Framework.Observability;

public static class RequestLoggingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSharedSerilogRequestLogging(this IApplicationBuilder app) =>
        UseGmaSerilogRequestLogging(app);

    public static IApplicationBuilder UseGmaSerilogRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                ModuleEndpointMetadata? module = httpContext.GetEndpoint()?.Metadata.GetMetadata<ModuleEndpointMetadata>();
                string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

                diagnosticContext.Set(ObservabilityLogPropertyNames.TraceId, traceId);

                if (module is not null)
                {
                    diagnosticContext.Set(ObservabilityLogPropertyNames.Module, module.ModuleName);
                }

                EnrichDiagnosticContext(diagnosticContext, httpContext);
            };
        });
    }

    private static void EnrichDiagnosticContext(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        foreach (IRequestLoggingDiagnosticContextContributor contributor in httpContext.RequestServices
                     .GetServices<IRequestLoggingDiagnosticContextContributor>())
        {
            try
            {
                contributor.Enrich(diagnosticContext, httpContext);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Request logging must stay best effort; request execution should not fail because enrichment failed.
            }
        }
    }
}

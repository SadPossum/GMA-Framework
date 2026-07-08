namespace Gma.Framework.Tenancy.Api.Serilog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Api.Serilog;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTenantSerilogRequestLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TenantSerilogRequestLoggingRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<TenantSerilogRequestLoggingRegistrationMarker>();
        builder.RequireFeature(new RequiredCompositionFeature(
            TenancyCompositionFeatures.Context,
            "Gma.Framework.Tenancy.Api.Serilog",
            optional: false,
            reason: "Tenant request-log enrichment needs an ITenantContext provider."));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IRequestLoggingDiagnosticContextContributor, TenantRequestLoggingDiagnosticContextContributor>());

        return builder;
    }

    private sealed class TenantSerilogRequestLoggingRegistrationMarker;
}

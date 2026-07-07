namespace Gma.Framework.Tenancy.Cqrs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTenantCqrsLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TenantCqrsLoggingRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<TenantCqrsLoggingRegistrationMarker>();
        builder.RequireFeature(new RequiredCompositionFeature(
            TenancyCompositionFeatures.Context,
            "Gma.Framework.Tenancy.Cqrs",
            optional: false,
            reason: "Tenant CQRS logging needs an ITenantContext provider."));
        builder.ProvideFeature(TenancyCqrsCompositionFeatures.LogScopeProvided("Gma.Framework.Tenancy.Cqrs"));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ICqrsLogScopeContributor, TenantCqrsLogScopeContributor>());

        return builder;
    }

    private sealed class TenantCqrsLoggingRegistrationMarker;
}

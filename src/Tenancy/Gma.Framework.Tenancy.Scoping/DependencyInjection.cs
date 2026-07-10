namespace Gma.Framework.Tenancy.Scoping;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Scoping;
using Gma.Framework.Scoping.Infrastructure;
using Gma.Framework.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTenantScoping(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddScopingInfrastructure();

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TenantScopingRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<TenantScopingRegistrationMarker>();
        builder.RequireFeature(new RequiredCompositionFeature(
            TenancyCompositionFeatures.Context,
            "Gma.Framework.Tenancy.Scoping",
            optional: false,
            reason: "Tenant scoping needs an ITenantContext/ITenantContextAccessor provider."));
        builder.ProvideFeature(ScopeCompositionFeatures.ContextProvided("Gma.Framework.Tenancy.Scoping"));
        builder.Services.Replace(ServiceDescriptor.Scoped<TenantScopeContext, TenantScopeContext>());
        builder.Services.Replace(ServiceDescriptor.Scoped<IScopeContext>(provider => provider.GetRequiredService<TenantScopeContext>()));
        builder.Services.Replace(ServiceDescriptor.Scoped<IScopeContextAccessor>(provider => provider.GetRequiredService<TenantScopeContext>()));

        return builder;
    }

    private sealed class TenantScopingRegistrationMarker;
}

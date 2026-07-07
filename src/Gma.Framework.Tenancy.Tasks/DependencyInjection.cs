namespace Gma.Framework.Tenancy.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTenantTaskExecutionContext(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TenantTaskExecutionContextRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<TenantTaskExecutionContextRegistrationMarker>();
        builder.RequireFeature(new RequiredCompositionFeature(
            TenancyCompositionFeatures.Context,
            "Gma.Framework.Tenancy.Tasks",
            optional: false,
            reason: "Tenant task execution context needs an ITenantContextAccessor provider."));
        builder.ProvideFeature(TasksCompositionFeatures.TenantScopeProvided("Gma.Framework.Tenancy.Tasks"));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ITaskExecutionContextContributor, TenantTaskExecutionContextContributor>());

        return builder;
    }

    private sealed class TenantTaskExecutionContextRegistrationMarker;
}

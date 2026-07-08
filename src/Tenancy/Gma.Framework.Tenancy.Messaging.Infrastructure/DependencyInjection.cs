namespace Gma.Framework.Tenancy.Messaging.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTenantAwareMessaging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TenantAwareMessagingRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<TenantAwareMessagingRegistrationMarker>();
        builder.RequireFeature(new RequiredCompositionFeature(
            TenancyCompositionFeatures.Context,
            "Gma.Framework.Tenancy.Messaging.Infrastructure",
            optional: false,
            reason: "Tenant-aware messaging needs an ITenantContext/ITenantContextAccessor provider."));
        builder.ProvideFeature(TenancyMessagingCompositionFeatures.TenantEventScopeProvided("Gma.Framework.Tenancy.Messaging.Infrastructure"));
        builder.ProvideFeature(TenancyMessagingCompositionFeatures.TenantConsumerContextProvided("Gma.Framework.Tenancy.Messaging.Infrastructure"));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IIntegrationEventScopeResolver, TenantIntegrationEventScopeResolver>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IIntegrationEventProcessingContextContributor, TenantIntegrationEventProcessingContextContributor>());

        return builder;
    }

    private sealed class TenantAwareMessagingRegistrationMarker;
}

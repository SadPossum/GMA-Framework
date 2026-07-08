namespace Gma.Framework.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Tenancy.Cqrs;
using Gma.Framework.Tenancy.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddSharedInfrastructure(this IHostApplicationBuilder builder) =>
        AddGmaInfrastructure(builder);

    public static IHostApplicationBuilder AddGmaInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(GmaInfrastructureRegistrationMarker)))
        {
            return builder;
        }

        builder.AddTenancyInfrastructure();
        builder.AddRuntimeInfrastructure();
        builder.AddApplicationEventsInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.AddTenantCqrsLogging();
        builder.Services.AddSingleton<GmaInfrastructureRegistrationMarker>();

        return builder;
    }

    private sealed class GmaInfrastructureRegistrationMarker;
}

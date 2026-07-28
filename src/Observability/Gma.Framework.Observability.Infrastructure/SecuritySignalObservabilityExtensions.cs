namespace Gma.Framework.Observability.Infrastructure;

using Gma.Framework.Observability;
using Gma.Framework.Runtime.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class SecuritySignalObservabilityExtensions
{
    public static IHostApplicationBuilder AddSecuritySignalObservability(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddRuntimeInfrastructure();
        builder.Services.AddSecuritySignalCore();
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType ==
                    typeof(SecuritySignalObservabilityRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<SecuritySignalObservabilityRegistrationMarker>();
        builder.Services.AddMetrics();
        builder.Services.TryAddSingleton<SecuritySignalRegistry>();
        builder.Services.TryAddSingleton<SecuritySignalMetrics>();
        builder.Services.Replace(
            ServiceDescriptor.Singleton<ISecuritySignalRecorder, SecuritySignalRecorder>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, SecuritySignalRegistryValidationService>());
        return builder;
    }

    private sealed class SecuritySignalObservabilityRegistrationMarker;
}

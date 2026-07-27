namespace Gma.Framework.RateLimiting.Infrastructure;

using Gma.Framework.ModuleComposition;
using Gma.Framework.RateLimiting;
using Gma.Framework.Runtime.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInMemoryRateLimiting(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(InMemoryRateLimitingRegistrationMarker)))
        {
            return builder;
        }

        EnsureNoProviderIsRegistered(builder.Services);
        builder.AddRuntimeInfrastructure();

        builder.Services.AddSingleton<InMemoryRateLimitingRegistrationMarker>();
        builder.Services.AddSingleton<IRateLimitProviderRegistration>(
            provider => provider.GetRequiredService<InMemoryRateLimitingRegistrationMarker>());
        builder.Services.AddSingleton<InMemoryMultiPartitionRateLimiter>();
        builder.Services.AddSingleton<IMultiPartitionRateLimiter>(
            provider => provider.GetRequiredService<InMemoryMultiPartitionRateLimiter>());
        builder.ProvideFeature(
            RateLimitingCompositionFeatures.ProviderProvided(
                "Gma.Framework.RateLimiting.Infrastructure"));

        return builder;
    }

    private static void EnsureNoProviderIsRegistered(IServiceCollection services)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(IMultiPartitionRateLimiter) ||
                descriptor.ServiceType == typeof(IRateLimitProviderRegistration)))
        {
            throw new InvalidOperationException(
                "Only one GMA multi-partition rate-limit provider can be registered.");
        }
    }

    private sealed class InMemoryRateLimitingRegistrationMarker
        : IRateLimitProviderRegistration
    {
        public string ProviderName => "in-memory";
        public bool IsDistributed => false;
    }
}

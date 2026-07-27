namespace Gma.Framework.RateLimiting.Redis;

using Gma.Framework.ModuleComposition;
using Gma.Framework.RateLimiting;
using Gma.Framework.Runtime.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddRedisRateLimiting(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(RedisRateLimitingRegistrationMarker)))
        {
            return builder;
        }

        EnsureNoProviderIsRegistered(builder.Services);

        IConfigurationSection section = builder.Configuration
            .GetSection(RedisRateLimitingOptions.SectionName);
        RedisRateLimitingOptions options =
            section.Get<RedisRateLimitingOptions>() ?? new RedisRateLimitingOptions();
        ValidateOptions(builder.Configuration, options);

        string connectionString = builder.Configuration.GetConnectionString(options.ConnectionName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{options.ConnectionName} is required when Redis rate limiting is enabled.");
        ConfigurationOptions configuration = ConfigurationOptions.Parse(connectionString);
        configuration.AbortOnConnectFail = false;

        builder.AddRuntimeInfrastructure();
        builder.Services.AddSingleton<RedisRateLimitingRegistrationMarker>();
        builder.Services.AddSingleton<IRateLimitProviderRegistration>(
            provider => provider.GetRequiredService<RedisRateLimitingRegistrationMarker>());
        builder.Services
            .AddOptions<RedisRateLimitingOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<RedisRateLimitingOptions>,
                RedisRateLimitingOptionsValidator>());
        builder.Services.AddSingleton(new RedisRateLimitingConnectionSettings(configuration));
        builder.Services.AddSingleton<RedisRateLimitingConnection>();
        builder.Services.AddSingleton<RedisRateLimitStorageKeyFormatter>();
        builder.Services.AddSingleton<RedisMultiPartitionRateLimiter>();
        builder.Services.AddSingleton<IMultiPartitionRateLimiter>(
            provider => provider.GetRequiredService<RedisMultiPartitionRateLimiter>());
        builder.ProvideFeature(
            RateLimitingCompositionFeatures.ProviderProvided(
                "Gma.Framework.RateLimiting.Redis"));
        builder.ProvideFeature(
            RateLimitingCompositionFeatures.DistributedProviderProvided(
                "Gma.Framework.RateLimiting.Redis"));

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

    private static void ValidateOptions(
        IConfiguration configuration,
        RedisRateLimitingOptions options)
    {
        ValidateOptionsResult result =
            new RedisRateLimitingOptionsValidator(configuration).Validate(name: null, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(
                RedisRateLimitingOptions.SectionName,
                typeof(RedisRateLimitingOptions),
                result.Failures);
        }
    }

    private sealed class RedisRateLimitingRegistrationMarker
        : IRateLimitProviderRegistration
    {
        public string ProviderName => "redis";
        public bool IsDistributed => true;
    }
}

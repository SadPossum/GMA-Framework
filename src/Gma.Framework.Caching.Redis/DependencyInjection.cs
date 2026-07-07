namespace Gma.Framework.Caching.Redis;

using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Caching;
using Gma.Framework.ModuleComposition;
using StackExchange.Redis;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddRedisCaching(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationSection caching = builder.Configuration.GetSection(CachingOptions.SectionName);
        CachingOptions cachingOptions = caching.Get<CachingOptions>() ?? new CachingOptions();

        if (!cachingOptions.Enabled)
        {
            return builder;
        }

        if (!Enum.IsDefined(cachingOptions.Provider) || cachingOptions.Provider == CacheProvider.Unknown)
        {
            throw new OptionsValidationException(
                CachingOptions.SectionName,
                typeof(CachingOptions),
                [$"{CachingOptions.SectionName}:Provider is not supported."]);
        }

        if (cachingOptions.Provider != CacheProvider.Redis)
        {
            return builder;
        }

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(RedisCachingRegistrationMarker)))
        {
            return builder;
        }

        IConfigurationSection redis = builder.Configuration.GetSection(RedisCachingOptions.SectionName);
        RedisCachingOptions redisOptions = redis.Get<RedisCachingOptions>() ?? new RedisCachingOptions();
        ValidateRedisOptions(builder.Configuration, redisOptions);

        string connectionString = builder.Configuration.GetConnectionString(redisOptions.ConnectionName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{redisOptions.ConnectionName} is required when Redis caching is enabled.");
        ConfigurationOptions configuration = ConfigurationOptions.Parse(connectionString);

        builder.Services.AddSingleton<RedisCachingRegistrationMarker>();
        builder.ProvideFeature(CachingCompositionFeatures.RedisProvided("Gma.Framework.Caching.Redis"));
        builder.Services.AddSingleton<IDistributedCacheAdapterRegistration>(
            provider => provider.GetRequiredService<RedisCachingRegistrationMarker>());
        builder.Services
            .AddOptions<RedisCachingOptions>()
            .Bind(redis)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RedisCachingOptions>, RedisCachingOptionsValidator>());

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = configuration;
            options.InstanceName = redisOptions.InstanceName;
        });

        return builder;
    }

    private static void ValidateRedisOptions(IConfiguration configuration, RedisCachingOptions options)
    {
        ValidateOptionsResult result = new RedisCachingOptionsValidator(configuration).Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(RedisCachingOptions.SectionName, typeof(RedisCachingOptions), result.Failures);
        }
    }

    private sealed class RedisCachingRegistrationMarker : IDistributedCacheAdapterRegistration;
}

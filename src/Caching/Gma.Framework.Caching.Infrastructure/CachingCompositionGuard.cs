namespace Gma.Framework.Caching.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Caching;

internal static class CachingCompositionGuard
{
    private const string MissingRedisAdapterMessage =
        "Redis caching is enabled but no distributed cache adapter is registered. Call AddRedisCaching() before AddCachingInfrastructure() or AddCachingCqrs().";

    public static CachingOptions EnsureValid(CachingOptions options, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (RequiresRedisAdapter(options) && serviceProvider.GetService<IDistributedCacheAdapterRegistration>() is null)
        {
            throw new InvalidOperationException(MissingRedisAdapterMessage);
        }

        return options;
    }

    private static bool RequiresRedisAdapter(CachingOptions options) =>
        options.Enabled && options.Provider == CacheProvider.Redis;
}

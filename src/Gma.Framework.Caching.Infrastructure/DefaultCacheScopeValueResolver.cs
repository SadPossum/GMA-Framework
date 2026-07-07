namespace Gma.Framework.Caching.Infrastructure;

using Gma.Framework.Caching;

internal sealed class DefaultCacheScopeValueResolver : ICacheScopeValueResolver
{
    public string Resolve(CacheScope scope) =>
        scope switch
        {
            CacheScope.Global => "global",
            CacheScope.Tenant => throw new InvalidOperationException(
                "A tenant-scoped cache key requires a tenant-aware cache scope resolver. Compose Gma.Framework.Tenancy.Caching for tenant-owned cache entries."),
            _ => throw new InvalidOperationException($"Unsupported cache scope '{scope}'.")
        };
}

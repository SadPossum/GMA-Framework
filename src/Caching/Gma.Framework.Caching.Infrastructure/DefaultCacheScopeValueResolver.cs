namespace Gma.Framework.Caching.Infrastructure;

using Gma.Framework.Caching;

internal sealed class DefaultCacheScopeValueResolver : ICacheScopeValueResolver
{
    public string Resolve(CacheScope scope) =>
        scope switch
        {
            CacheScope.Global => "global",
            CacheScope.Scope => throw new InvalidOperationException(
                "A scope-aware cache key requires a cache scope resolver. Compose a scoping provider such as Gma.Framework.Tenancy.Caching for scope-aware cache entries."),
            _ => throw new InvalidOperationException($"Unsupported cache scope '{scope}'.")
        };
}

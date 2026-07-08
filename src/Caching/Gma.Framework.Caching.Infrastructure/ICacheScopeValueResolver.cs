namespace Gma.Framework.Caching.Infrastructure;

using Gma.Framework.Caching;

public interface ICacheScopeValueResolver
{
    string Resolve(CacheScope scope);
}

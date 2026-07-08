namespace Gma.Framework.Caching;

#pragma warning disable CA1711 // Queue describes the deferred invalidation semantics of this public contract.
public interface ICacheInvalidationQueue
{
    void Remove(CacheKey key);
    void RemoveByTag(CacheTag tag);
}
#pragma warning restore CA1711

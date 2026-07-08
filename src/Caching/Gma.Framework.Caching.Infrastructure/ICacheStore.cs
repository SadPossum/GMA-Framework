namespace Gma.Framework.Caching.Infrastructure;

using Gma.Framework.Caching;

internal interface ICacheStore
{
    ValueTask RemoveAsync(CacheKey key, CancellationToken cancellationToken);
    ValueTask RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken);
}

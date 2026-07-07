namespace Gma.Framework.Caching.Infrastructure;

using Gma.Framework.Caching;

internal interface ICacheInvalidationQueueFlusher
{
    ValueTask FlushAsync(CancellationToken cancellationToken);
}

internal sealed class CacheInvalidationQueue(ICacheStore cacheStore) : ICacheInvalidationQueue, ICacheInvalidationQueueFlusher
{
    private readonly List<CacheKey> keys = [];
    private readonly List<CacheTag> tags = [];

    public void Remove(CacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        this.keys.Add(key);
    }

    public void RemoveByTag(CacheTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        this.tags.Add(tag);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        int keyCount = this.keys.Count;
        for (int index = 0; index < keyCount; index++)
        {
            CacheKey key = this.keys[0];
            await cacheStore.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            this.keys.RemoveAt(0);
        }

        int tagCount = this.tags.Count;
        for (int index = 0; index < tagCount; index++)
        {
            CacheTag tag = this.tags[0];
            await cacheStore.RemoveByTagAsync(tag, cancellationToken).ConfigureAwait(false);
            this.tags.RemoveAt(0);
        }
    }
}

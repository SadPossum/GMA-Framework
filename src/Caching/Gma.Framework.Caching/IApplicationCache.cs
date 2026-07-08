namespace Gma.Framework.Caching;

public interface IApplicationCache
{
    ValueTask<T> GetOrCreateAsync<T>(
        CacheKey key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryPolicy? policy = null,
        IReadOnlyCollection<CacheTag>? tags = null,
        CancellationToken cancellationToken = default);
}

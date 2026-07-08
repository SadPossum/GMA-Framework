namespace Gma.Framework.Caching.Infrastructure;

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Caching;

internal sealed class HybridApplicationCache(
    HybridCache cache,
    CacheKeyFormatter keyFormatter,
    CacheMetrics metrics,
    IOptions<CachingOptions> options,
    IServiceProvider serviceProvider,
    ILogger<HybridApplicationCache> logger) : IApplicationCache, ICacheStore
{
    private readonly CachingOptions cachingOptions = CachingCompositionGuard.EnsureValid(options.Value, serviceProvider);

    public async ValueTask<T> GetOrCreateAsync<T>(
        CacheKey key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryPolicy? policy = null,
        IReadOnlyCollection<CacheTag>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        ValidateTags(tags);
        cancellationToken.ThrowIfCancellationRequested();

        Stopwatch stopwatch = Stopwatch.StartNew();
        string provider = this.cachingOptions.Enabled
            ? this.cachingOptions.Provider.ToString().ToLowerInvariant()
            : "disabled";
        string physicalKey = keyFormatter.Format(key);
        string[] physicalTags = tags?.Select(keyFormatter.Format).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (!this.cachingOptions.Enabled)
        {
            try
            {
                return await factory(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                this.TryRecordRequest(key.Module, provider, "bypass", stopwatch.Elapsed);
            }
        }

        CacheEntryPolicy effectivePolicy = policy ?? new CacheEntryPolicy(
            this.cachingOptions.DefaultDistributedExpiration,
            this.cachingOptions.DefaultLocalExpiration);
        HybridCacheEntryFlags flags = effectivePolicy.LocalCacheEnabled
            ? HybridCacheEntryFlags.None
            : HybridCacheEntryFlags.DisableLocalCache;
        HybridCacheEntryOptions entryOptions = new()
        {
            Expiration = effectivePolicy.DistributedExpiration,
            LocalCacheExpiration = effectivePolicy.LocalExpiration,
            Flags = flags
        };
        FactoryState<T> state = new(factory);

        try
        {
            T value = await cache.GetOrCreateAsync(
                physicalKey,
                state,
                static async (factoryState, token) => await factoryState.InvokeAsync(token).ConfigureAwait(false),
                entryOptions,
                physicalTags,
                cancellationToken).ConfigureAwait(false);
            this.TryRecordRequest(key.Module, provider, state.WasInvoked ? "miss" : "hit", stopwatch.Elapsed);
            return value;
        }
        catch (SourceFactoryException exception)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this.LogBackendFailure(key, "read", provider, exception);
            this.TryRecordRequest(key.Module, provider, "bypass", stopwatch.Elapsed);

            if (state.HasValue)
            {
                return state.Value!;
            }

            return await factory(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        string physicalKey = keyFormatter.Format(key);

        if (!this.cachingOptions.Enabled)
        {
            return;
        }

        try
        {
            await cache.RemoveAsync(physicalKey, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this.LogInvalidationFailure(key.Module, "key", key, exception);
        }
    }

    public async ValueTask RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tag);
        string physicalTag = keyFormatter.Format(tag);

        if (!this.cachingOptions.Enabled)
        {
            return;
        }

        try
        {
            await cache.RemoveByTagAsync(physicalTag, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this.LogInvalidationFailure(tag.Module, "tag", tag, exception);
        }
    }

    private void LogBackendFailure(CacheKey key, string operation, string provider, Exception exception)
    {
        this.TryRecordBackendFailure(key.Module, operation, provider);

        try
        {
            logger.LogWarning(
                exception,
                "Cache {Operation} failed open for module {Module}, entry {CacheEntry}, scope {CacheScope} and segments {@CacheSegments}",
                operation,
                key.Module,
                key.Entry,
                key.Scope,
                key.Segments);
        }
        catch (Exception)
        {
            // Cache fail-open paths must not fail closed because an observability provider is unavailable.
        }
    }

    private void LogInvalidationFailure(string module, string operation, object cacheIdentity, Exception exception)
    {
        string provider = this.cachingOptions.Provider.ToString().ToLowerInvariant();
        this.TryRecordInvalidationFailure(module, operation, provider);

        try
        {
            logger.LogWarning(
                exception,
                "Cache {Operation} invalidation failed open for module {Module} and identity {@CacheIdentity}",
                operation,
                module,
                cacheIdentity);
        }
        catch (Exception)
        {
            // Cache fail-open paths must not fail closed because an observability provider is unavailable.
        }
    }

    private void TryRecordRequest(string module, string provider, string result, TimeSpan elapsed)
    {
        try
        {
            metrics.RecordRequest(module, provider, result, elapsed);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not affect cache reads.
        }
    }

    private void TryRecordBackendFailure(string module, string operation, string provider)
    {
        try
        {
            metrics.RecordBackendFailure(module, operation, provider);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not affect fail-open behavior.
        }
    }

    private void TryRecordInvalidationFailure(string module, string operation, string provider)
    {
        try
        {
            metrics.RecordInvalidationFailure(module, operation, provider);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not affect cache invalidation.
        }
    }

    private static void ValidateTags(IReadOnlyCollection<CacheTag>? tags)
    {
        if (tags is null)
        {
            return;
        }

        if (tags.Any(tag => tag is null))
        {
            throw new ArgumentException("Cache tags must not contain null entries.", nameof(tags));
        }
    }

    private sealed class FactoryState<T>(Func<CancellationToken, ValueTask<T>> factory)
    {
        public bool WasInvoked { get; private set; }
        public bool HasValue { get; private set; }
        public T? Value { get; private set; }

        public async ValueTask<T> InvokeAsync(CancellationToken cancellationToken)
        {
            this.WasInvoked = true;

            try
            {
                this.Value = await factory(cancellationToken).ConfigureAwait(false);
                this.HasValue = true;
                return this.Value;
            }
            catch (Exception exception) when (exception is not SourceFactoryException)
            {
                throw new SourceFactoryException(exception);
            }
        }
    }

    private sealed class SourceFactoryException(Exception innerException)
        : Exception("The cache source factory failed.", innerException);
}

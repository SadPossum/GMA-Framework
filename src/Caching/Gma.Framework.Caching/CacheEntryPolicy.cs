namespace Gma.Framework.Caching;

public sealed record CacheEntryPolicy
{
    public CacheEntryPolicy(
        TimeSpan distributedExpiration,
        TimeSpan localExpiration,
        bool localCacheEnabled = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(distributedExpiration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(localExpiration, TimeSpan.Zero);

        if (localCacheEnabled && localExpiration > distributedExpiration)
        {
            throw new ArgumentException(
                "Local expiration cannot exceed distributed expiration.",
                nameof(localExpiration));
        }

        this.DistributedExpiration = distributedExpiration;
        this.LocalExpiration = localExpiration;
        this.LocalCacheEnabled = localCacheEnabled;
    }

    public TimeSpan DistributedExpiration { get; }
    public TimeSpan LocalExpiration { get; }
    public bool LocalCacheEnabled { get; }
}

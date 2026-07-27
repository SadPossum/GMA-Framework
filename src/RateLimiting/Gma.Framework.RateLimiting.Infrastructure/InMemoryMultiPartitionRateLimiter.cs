namespace Gma.Framework.RateLimiting.Infrastructure;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.RateLimiting;
using Gma.Framework.Runtime.Time;

internal sealed class InMemoryMultiPartitionRateLimiter(
    ISystemClock clock)
    : IMultiPartitionRateLimiter
{
    private const int LockStripeCount = 64;
    private const int CleanupFrequency = 1_024;
    private const int CleanupScanLimit = 256;

    private readonly ConcurrentDictionary<string, WindowState> windows =
        new(StringComparer.Ordinal);
    private readonly object[] lockStripes =
        Enumerable.Range(0, LockStripeCount).Select(_ => new object()).ToArray();
    private int acquisitionCount;

    public ValueTask<MultiPartitionRateLimitDecision> AcquireAsync(
        MultiPartitionRateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        long nowMilliseconds = clock.UtcNow.ToUnixTimeMilliseconds();
        PartitionState[] partitions = request.Partitions
            .Select(partition => PartitionState.Create(partition, nowMilliseconds))
            .ToArray();
        int[] stripeIndexes = partitions
            .Select(partition => GetStripeIndex(partition.StorageKey))
            .Distinct()
            .Order()
            .ToArray();

        this.EnterStripes(stripeIndexes);
        MultiPartitionRateLimitDecision decision;
        try
        {
            long retryAfterMilliseconds = 0;
            foreach (PartitionState partition in partitions)
            {
                WindowState current = this.GetCurrentState(partition);
                if ((long)current.PermitCount + request.PermitCount > partition.PermitLimit)
                {
                    retryAfterMilliseconds = Math.Max(
                        retryAfterMilliseconds,
                        partition.ExpiresAtMilliseconds - nowMilliseconds);
                }
            }

            if (retryAfterMilliseconds > 0)
            {
                decision = MultiPartitionRateLimitDecision.Rejected(
                    TimeSpan.FromMilliseconds(retryAfterMilliseconds));
            }
            else
            {
                foreach (PartitionState partition in partitions)
                {
                    WindowState current = this.GetCurrentState(partition);
                    this.windows[partition.StorageKey] = current with
                    {
                        PermitCount = current.PermitCount + request.PermitCount
                    };
                }

                decision = MultiPartitionRateLimitDecision.Acquired();
            }
        }
        finally
        {
            this.ExitStripes(stripeIndexes);
        }

        this.TryCleanup(nowMilliseconds);
        return ValueTask.FromResult(decision);
    }

    private WindowState GetCurrentState(PartitionState partition)
    {
        if (this.windows.TryGetValue(partition.StorageKey, out WindowState? current) &&
            current is not null &&
            current.Bucket == partition.Bucket)
        {
            return current;
        }

        return new WindowState(
            partition.Bucket,
            PermitCount: 0,
            partition.ExpiresAtMilliseconds);
    }

    private void TryCleanup(long nowMilliseconds)
    {
        if (Interlocked.Increment(ref this.acquisitionCount) % CleanupFrequency != 0)
        {
            return;
        }

        int scanned = 0;
        foreach (KeyValuePair<string, WindowState> entry in this.windows)
        {
            if (scanned++ >= CleanupScanLimit)
            {
                break;
            }

            if (entry.Value.ExpiresAtMilliseconds <= nowMilliseconds)
            {
                _ = ((ICollection<KeyValuePair<string, WindowState>>)this.windows).Remove(entry);
            }
        }
    }

    private static int GetStripeIndex(string storageKey) =>
        (storageKey.GetHashCode(StringComparison.Ordinal) & int.MaxValue) % LockStripeCount;

    private void EnterStripes(IEnumerable<int> stripeIndexes)
    {
        foreach (int stripeIndex in stripeIndexes)
        {
            Monitor.Enter(this.lockStripes[stripeIndex]);
        }
    }

    private void ExitStripes(IEnumerable<int> stripeIndexes)
    {
        foreach (int stripeIndex in stripeIndexes.Reverse())
        {
            Monitor.Exit(this.lockStripes[stripeIndex]);
        }
    }

    private sealed record PartitionState(
        string StorageKey,
        int PermitLimit,
        long Bucket,
        long ExpiresAtMilliseconds)
    {
        public static PartitionState Create(
            FixedWindowRateLimitPartition partition,
            long nowMilliseconds)
        {
            long windowMilliseconds = checked((long)partition.Window.TotalMilliseconds);
            long bucket = Math.DivRem(nowMilliseconds, windowMilliseconds, out long elapsed);
            long expiresAtMilliseconds = checked(nowMilliseconds + windowMilliseconds - elapsed);
            string descriptor = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{partition.Identity}|{partition.PermitLimit}|{windowMilliseconds}");
            string storageKey = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(descriptor)))
                .ToLowerInvariant();

            return new PartitionState(
                storageKey,
                partition.PermitLimit,
                bucket,
                expiresAtMilliseconds);
        }
    }

    private sealed record WindowState(
        long Bucket,
        int PermitCount,
        long ExpiresAtMilliseconds);
}

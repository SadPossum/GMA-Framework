namespace Gma.Framework.RateLimiting;

public interface IMultiPartitionRateLimiter
{
    ValueTask<MultiPartitionRateLimitDecision> AcquireAsync(
        MultiPartitionRateLimitRequest request,
        CancellationToken cancellationToken = default);
}

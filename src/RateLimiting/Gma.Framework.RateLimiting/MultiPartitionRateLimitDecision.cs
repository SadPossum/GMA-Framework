namespace Gma.Framework.RateLimiting;

public enum MultiPartitionRateLimitOutcome
{
    Unknown = 0,
    Acquired = 1,
    Rejected = 2,
    ProviderUnavailable = 3
}

public sealed record MultiPartitionRateLimitDecision
{
    private MultiPartitionRateLimitDecision(
        MultiPartitionRateLimitOutcome outcome,
        TimeSpan? retryAfter)
    {
        this.Outcome = outcome;
        this.RetryAfter = retryAfter;
    }

    public MultiPartitionRateLimitOutcome Outcome { get; }
    public TimeSpan? RetryAfter { get; }

    public static MultiPartitionRateLimitDecision Acquired() =>
        new(MultiPartitionRateLimitOutcome.Acquired, retryAfter: null);

    public static MultiPartitionRateLimitDecision Rejected(TimeSpan retryAfter)
    {
        if (retryAfter <= TimeSpan.Zero ||
            retryAfter > FixedWindowRateLimitPartition.MaximumWindow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                $"Retry delays must be greater than zero and no longer than {FixedWindowRateLimitPartition.MaximumWindow}.");
        }

        return new(MultiPartitionRateLimitOutcome.Rejected, retryAfter);
    }

    public static MultiPartitionRateLimitDecision ProviderUnavailable() =>
        new(MultiPartitionRateLimitOutcome.ProviderUnavailable, retryAfter: null);
}

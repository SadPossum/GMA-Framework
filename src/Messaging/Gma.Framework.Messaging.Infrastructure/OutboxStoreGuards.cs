namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Runtime.Workers;
public static class OutboxStoreGuards
{
    public static (int BatchSize, string WorkerId, DateTimeOffset NowUtc, TimeSpan LockDuration) ValidateClaimArguments(
        int batchSize,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan lockDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        DateTimeOffset normalizedNowUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockDuration, TimeSpan.Zero);

        return (batchSize, WorkerIds.Normalize(workerId), normalizedNowUtc, lockDuration);
    }

    public static (Guid Id, string WorkerId, DateTimeOffset NowUtc) ValidateMarkArguments(
        Guid id,
        string workerId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id must not be empty.", nameof(id));
        }

        return (id, WorkerIds.Normalize(workerId), RequireTimestamp(nowUtc, nameof(nowUtc)));
    }

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName) =>
        value == default
            ? throw new ArgumentException($"{parameterName} must not be the default timestamp.", parameterName)
            : value;
}

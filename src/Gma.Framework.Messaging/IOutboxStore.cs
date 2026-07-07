namespace Gma.Framework.Messaging;

public interface IOutboxStore
{
    string ModuleName { get; }
    Task<IReadOnlyList<OutboxMessageRecord>> ClaimPendingAsync(
        int batchSize,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        Guid id,
        string workerId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid id,
        string workerId,
        string error,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

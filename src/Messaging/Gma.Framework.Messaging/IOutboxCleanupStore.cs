namespace Gma.Framework.Messaging;

public interface IOutboxCleanupStore
{
    string ModuleName { get; }

    Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset processedBeforeUtc,
        int maxMessages,
        CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetOldestProcessedAtUtcAsync(CancellationToken cancellationToken);
}

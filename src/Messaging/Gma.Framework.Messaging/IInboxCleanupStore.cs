namespace Gma.Framework.Messaging;

public interface IInboxCleanupStore
{
    string ModuleName { get; }

    Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset processedBeforeUtc,
        int maxMessages,
        CancellationToken cancellationToken);
}

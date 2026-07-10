namespace Gma.Framework.Messaging;

public interface IOutboxBacklogReader
{
    string ModuleName { get; }

    Task<OutboxBacklogSnapshot> GetBacklogAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed record OutboxBacklogSnapshot(
    string ModuleName,
    long PendingCount,
    long ExhaustedCount,
    DateTimeOffset? OldestPendingAtUtc,
    DateTimeOffset CapturedAtUtc)
{
    public TimeSpan OldestPendingAge => this.OldestPendingAtUtc is null
        ? TimeSpan.Zero
        : this.CapturedAtUtc - this.OldestPendingAtUtc.Value;
}

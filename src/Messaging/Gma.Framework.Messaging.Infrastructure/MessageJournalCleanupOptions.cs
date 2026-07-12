namespace Gma.Framework.Messaging.Infrastructure;

public sealed class MessageJournalCleanupOptions
{
    public const string SectionName = "MessageJournalCleanup";

    public bool Enabled { get; set; }
    public bool CleanupProcessedOutbox { get; set; } = true;
    public bool CleanupProcessedInbox { get; set; } = true;
    public TimeSpan ProcessedOutboxRetention { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan ProcessedInboxRetention { get; set; } = TimeSpan.FromDays(14);
    public TimeSpan BrokerReplayHorizon { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    public int BatchSize { get; set; } = 500;
    public int MaxBatchesPerStorePerCycle { get; set; } = 10;
}

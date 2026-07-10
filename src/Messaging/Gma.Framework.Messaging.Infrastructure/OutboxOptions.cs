namespace Gma.Framework.Messaging.Infrastructure;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 25;
    public int PollIntervalMilliseconds { get; set; } = 5_000;
    public int LockDurationMilliseconds { get; set; } = 60_000;
    public int MaxAttempts { get; set; } = 10;
    public int BacklogPollIntervalSeconds { get; set; } = 30;

    public TimeSpan EffectivePollInterval => TimeSpan.FromMilliseconds(Math.Max(1, this.PollIntervalMilliseconds));
    public TimeSpan EffectiveLockDuration => TimeSpan.FromMilliseconds(Math.Max(1, this.LockDurationMilliseconds));
    public int EffectiveBatchSize => Math.Max(1, this.BatchSize);
    public int EffectiveMaxAttempts => Math.Max(1, this.MaxAttempts);
    public TimeSpan EffectiveBacklogPollInterval => TimeSpan.FromSeconds(Math.Max(1, this.BacklogPollIntervalSeconds));
}

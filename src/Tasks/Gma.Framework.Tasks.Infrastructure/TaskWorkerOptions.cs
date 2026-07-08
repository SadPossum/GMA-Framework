namespace Gma.Framework.Tasks.Infrastructure;

using Gma.Framework.Tasks;

public sealed class TaskWorkerOptions
{
    public const string SectionName = "Tasks:Worker";

    public bool Enabled { get; set; }
    public string[] WorkerGroups { get; set; } = [TaskWorkerGroups.Default];
    public string? WorkerId { get; set; }
    public string? NodeId { get; set; }
    public int BatchSize { get; set; } = 10;
    public int MaxConcurrency { get; set; } = 1;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    public bool TimeoutScannerEnabled { get; set; } = true;
    public TimeSpan TimeoutScannerPollInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan StaleHeartbeatTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int TimeoutScannerBatchSize { get; set; } = 100;
    public bool MetricsSamplerEnabled { get; set; } = true;
    public TimeSpan MetricsSamplerPollInterval { get; set; } = TimeSpan.FromSeconds(15);

    public int EffectiveBatchSize => Math.Clamp(this.BatchSize, 1, 500);
    public int EffectiveMaxConcurrency => Math.Clamp(this.MaxConcurrency, 1, 100);
    public TimeSpan EffectivePollInterval => this.PollInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : this.PollInterval;
    public TimeSpan EffectiveLeaseDuration => this.LeaseDuration <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : this.LeaseDuration;
    public TimeSpan EffectiveHandlerTimeout => this.HandlerTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : this.HandlerTimeout;
    public TimeSpan EffectiveRetryBaseDelay => this.RetryBaseDelay <= TimeSpan.Zero ? TimeSpan.FromSeconds(2) : this.RetryBaseDelay;
    public TimeSpan EffectiveRetryMaxDelay => this.RetryMaxDelay <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : this.RetryMaxDelay;
    public TimeSpan EffectiveTimeoutScannerPollInterval => this.TimeoutScannerPollInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : this.TimeoutScannerPollInterval;
    public TimeSpan EffectiveStaleHeartbeatTimeout => this.StaleHeartbeatTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : this.StaleHeartbeatTimeout;
    public int EffectiveTimeoutScannerBatchSize => Math.Clamp(this.TimeoutScannerBatchSize, 1, 500);
    public TimeSpan EffectiveMetricsSamplerPollInterval => this.MetricsSamplerPollInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(15) : this.MetricsSamplerPollInterval;

    public IReadOnlyList<string> EffectiveWorkerGroups =>
        this.WorkerGroups is { Length: > 0 }
            ? this.WorkerGroups
                .Select(group => TaskNames.NormalizeWorkerGroup(group, nameof(this.WorkerGroups)))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [TaskWorkerGroups.Default];
}

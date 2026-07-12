namespace Gma.Framework.Messaging.Nats;

public sealed class NatsConsumerOptions
{
    public const string SectionName = "NatsConsumers";

    public bool Enabled { get; set; }
    public string? DurablePrefix { get; set; }
    public int FetchBatchSize { get; set; } = 10;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan AckWait { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan? AckProgressInterval { get; set; }
    public int MaxDeliver { get; set; } = 5;
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan NakDelay { get; set; } = TimeSpan.FromSeconds(1);

    public int EffectiveFetchBatchSize => Math.Clamp(this.FetchBatchSize, 1, 500);
    public int EffectiveMaxDeliver => Math.Max(1, this.MaxDeliver);
    public TimeSpan EffectivePollInterval => this.PollInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : this.PollInterval;
    public TimeSpan EffectiveFetchExpires =>
        this.EffectivePollInterval <= TimeSpan.FromSeconds(1)
            ? TimeSpan.FromMilliseconds(1100)
            : this.EffectivePollInterval;
    public TimeSpan EffectiveAckWait => this.AckWait <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : this.AckWait;
    public TimeSpan EffectiveAckProgressInterval => this.AckProgressInterval ??
        TimeSpan.FromTicks(Math.Max(1, this.EffectiveAckWait.Ticks / 3));
    public TimeSpan EffectiveHandlerTimeout => this.HandlerTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : this.HandlerTimeout;
    public TimeSpan EffectiveNakDelay => this.NakDelay <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : this.NakDelay;
    public string EffectiveDurablePrefix(string applicationNamespace) =>
        string.IsNullOrWhiteSpace(this.DurablePrefix)
            ? applicationNamespace
            : this.DurablePrefix;
}

namespace Gma.Framework.Tests.Messaging;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class MessageJournalCleanupTests
{
    [Fact]
    public void Options_validator_accepts_safe_disabled_defaults()
    {
        ValidateOptionsResult result = new MessageJournalCleanupOptionsValidator()
            .Validate(null, new MessageJournalCleanupOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Options_validator_rejects_inbox_retention_shorter_than_replay_horizon()
    {
        MessageJournalCleanupOptions options = new()
        {
            ProcessedInboxRetention = TimeSpan.FromDays(6),
            BrokerReplayHorizon = TimeSpan.FromDays(7),
        };

        ValidateOptionsResult result = new MessageJournalCleanupOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("BrokerReplayHorizon", StringComparison.Ordinal));
    }

    [Fact]
    public void Options_validator_rejects_enabled_cleanup_without_a_journal()
    {
        MessageJournalCleanupOptions options = new()
        {
            Enabled = true,
            CleanupProcessedOutbox = false,
            CleanupProcessedInbox = false,
        };

        ValidateOptionsResult result = new MessageJournalCleanupOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public async Task Cleanup_is_bounded_and_one_module_failure_does_not_block_other_modules()
    {
        TestOutboxCleanupStore failing = new("failing", remaining: 10, fail: true);
        TestOutboxCleanupStore healthy = new("healthy", remaining: 10);
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MessageJournalCleanup:Enabled"] = "true",
            ["MessageJournalCleanup:CleanupProcessedOutbox"] = "true",
            ["MessageJournalCleanup:CleanupProcessedInbox"] = "false",
            ["MessageJournalCleanup:CleanupInterval"] = "00:01:00",
            ["MessageJournalCleanup:BatchSize"] = "2",
            ["MessageJournalCleanup:MaxBatchesPerStorePerCycle"] = "3",
        });
        builder.Services.AddSingleton<IOutboxStore>(failing);
        builder.Services.AddSingleton<IOutboxStore>(healthy);
        builder.AddMessagingInfrastructure();
        using IHost host = builder.Build();

        await host.StartAsync();
        Assert.True(await healthy.WaitForCallsAsync(3, TimeSpan.FromSeconds(2)));
        await host.StopAsync();

        Assert.Equal(1, failing.CallCount);
        Assert.Equal(3, healthy.CallCount);
        Assert.Equal(6, healthy.DeletedCount);
        Assert.All(healthy.BatchSizes, batchSize => Assert.Equal(2, batchSize));
    }

    private sealed class TestOutboxCleanupStore(
        string moduleName,
        int remaining,
        bool fail = false) : IOutboxStore, IOutboxCleanupStore
    {
        private readonly Lock sync = new();
        private readonly List<int> batchSizes = [];
        private int callCount;
        private int deletedCount;
        private int remaining = remaining;

        public string ModuleName { get; } = moduleName;
        public int CallCount => Volatile.Read(ref this.callCount);
        public int DeletedCount => Volatile.Read(ref this.deletedCount);

        public IReadOnlyList<int> BatchSizes
        {
            get
            {
                lock (this.sync)
                {
                    return this.batchSizes.ToArray();
                }
            }
        }

        public Task<int> DeleteProcessedBeforeAsync(
            DateTimeOffset processedBeforeUtc,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.callCount);
            lock (this.sync)
            {
                this.batchSizes.Add(maxMessages);
            }

            if (fail)
            {
                throw new InvalidOperationException("Store unavailable.");
            }

            int deleted = Math.Min(maxMessages, this.remaining);
            this.remaining -= deleted;
            Interlocked.Add(ref this.deletedCount, deleted);
            return Task.FromResult(deleted);
        }

        public Task<bool> WaitForCallsAsync(int expected, TimeSpan timeout) =>
            WaitForCountAsync(() => this.CallCount, expected, timeout);

        public Task<IReadOnlyList<OutboxMessageRecord>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan lockDuration,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkProcessedAsync(
            Guid id,
            string workerId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkFailedAsync(
            Guid id,
            string workerId,
            string error,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static async Task<bool> WaitForCountAsync(
        Func<int> read,
        int expected,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (read() >= expected)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
        }

        return read() >= expected;
    }
}

namespace Gma.Framework.Tests.Realtime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Realtime;
using Gma.Framework.Realtime.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RealtimeTests
{
    [Fact]
    public async Task In_memory_bus_delivers_messages_to_matching_channel_only()
    {
        using IHost host = BuildHost();
        IRealtimeFeed<string> feed = host.Services.GetRequiredService<IRealtimeFeed<string>>();
        IRealtimeSink<string> sink = host.Services.GetRequiredService<IRealtimeSink<string>>();
        RealtimeChannel matching = RealtimeChannel.Create("files-upload", "tenant-a", "upload-a");
        RealtimeChannel other = RealtimeChannel.Create("files-upload", "tenant-a", "upload-b");
        await using IRealtimeSubscription<string> matchingSubscription = feed.Subscribe(matching);
        await using IRealtimeSubscription<string> otherSubscription = feed.Subscribe(other);

        await sink.DeliverAsync(matching, "ready", CancellationToken.None);

        Assert.Equal("ready", await ReadOneAsync(matchingSubscription));
        await AssertNoMessageAsync(otherSubscription);
    }

    [Fact]
    public async Task Slow_subscribers_keep_bounded_queue_and_drop_oldest_message()
    {
        using IHost host = BuildHost(capacity: 1);
        IRealtimeFeed<string> feed = host.Services.GetRequiredService<IRealtimeFeed<string>>();
        IRealtimeSink<string> sink = host.Services.GetRequiredService<IRealtimeSink<string>>();
        RealtimeChannel channel = RealtimeChannel.Create("tasks-run", "tenant-a", "run-a");
        await using IRealtimeSubscription<string> subscription = feed.Subscribe(channel);

        await sink.DeliverAsync(channel, "first", CancellationToken.None);
        await sink.DeliverAsync(channel, "second", CancellationToken.None);

        Assert.Equal("second", await ReadOneAsync(subscription));
    }

    [Fact]
    public void Realtime_channels_require_name_and_routing_segments()
    {
        Assert.ThrowsAny<ArgumentException>(() => RealtimeChannel.Create("files upload", "tenant-a"));
        Assert.ThrowsAny<ArgumentException>(() => RealtimeChannel.Create("files-upload"));
        Assert.ThrowsAny<ArgumentException>(() => RealtimeChannel.Create("files-upload", "tenant-a", " "));
    }

    [Fact]
    public void Realtime_channels_are_immutable_value_objects()
    {
        RealtimeChannel channel = RealtimeChannel.Create("files-upload", "tenant-a", "upload-a");
        RealtimeChannel same = RealtimeChannel.Create("files-upload", "tenant-a", "upload-a");
        RealtimeChannel different = RealtimeChannel.Create("files-upload", "tenant-a", "upload-b");

        Assert.Equal(channel, same);
        Assert.Equal(channel.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(channel, different);
        IList<string> mutableView = Assert.IsType<IList<string>>(channel.Segments, exactMatch: false);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = "tenant-b");
        Assert.Equal("tenant-a", channel.Segments[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RealtimeSubscriberQueueOptions<string>.MaximumSubscriberQueueCapacity + 1)]
    public async Task Invalid_queue_capacity_fails_startup(int capacity)
    {
        using IHost host = BuildHost(capacity);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Disposed_subscriptions_complete_their_reader_and_stop_delivery()
    {
        using IHost host = BuildHost();
        IRealtimeFeed<string> feed = host.Services.GetRequiredService<IRealtimeFeed<string>>();
        IRealtimeSink<string> sink = host.Services.GetRequiredService<IRealtimeSink<string>>();
        RealtimeChannel channel = RealtimeChannel.Create("tasks-run", "tenant-a", "run-a");
        IRealtimeSubscription<string> subscription = feed.Subscribe(channel);

        await subscription.DisposeAsync();
        await sink.DeliverAsync(channel, "ignored", CancellationToken.None);

        await AssertCompletedAsync(subscription);
    }

    [Fact]
    public async Task Disposed_bus_rejects_new_subscriptions()
    {
        using IHost host = BuildHost();
        IRealtimeFeed<string> feed = host.Services.GetRequiredService<IRealtimeFeed<string>>();
        IRealtimeSink<string> sink = host.Services.GetRequiredService<IRealtimeSink<string>>();
        RealtimeChannel channel = RealtimeChannel.Create("tasks-run", "tenant-a", "run-a");

        host.Dispose();

        Assert.Throws<ObjectDisposedException>(() => feed.Subscribe(channel));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await sink.DeliverAsync(channel, "ignored", CancellationToken.None);
        });
    }

    private static IHost BuildHost(int capacity = RealtimeSubscriberQueueOptions<string>.DefaultSubscriberQueueCapacity)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddOptions<RealtimeSubscriberQueueOptions<string>>()
            .Configure(options => options.SubscriberQueueCapacity = capacity);
        builder.Services.AddInMemoryRealtimeBus<string>();

        return builder.Build();
    }

    private static async Task<string> ReadOneAsync(IRealtimeSubscription<string> subscription)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        await using IAsyncEnumerator<string> messages =
            subscription.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);

        Assert.True(await messages.MoveNextAsync().ConfigureAwait(false));
        return messages.Current;
    }

    private static async Task AssertNoMessageAsync(IRealtimeSubscription<string> subscription)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromMilliseconds(150));
        IAsyncEnumerator<string> messages =
            subscription.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);

        try
        {
            bool received = await messages.MoveNextAsync().ConfigureAwait(false);
            Assert.False(received);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task AssertCompletedAsync(IRealtimeSubscription<string> subscription)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        await using IAsyncEnumerator<string> messages =
            subscription.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);

        Assert.False(await messages.MoveNextAsync().ConfigureAwait(false));
    }
}

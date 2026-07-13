namespace Gma.Framework.Tests.Notifications;

using System.Text.Json;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Email;
using Gma.Framework.Modules;
using Gma.Framework.Notifications;
using Gma.Framework.Notifications.Api;
using Gma.Framework.Notifications.Cqrs;
using Gma.Framework.Notifications.Infrastructure;
using Gma.Framework.Realtime.Notifications;
using Gma.Framework.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

[Trait("Category", "Unit")]
public sealed class UserNotificationsTests
{
    [Fact]
    public void Email_send_request_requires_a_recipient_address()
    {
        Assert.Throws<ArgumentException>(() => new EmailSendRequest(
            " ",
            "Security alert",
            "A new session was created.",
            htmlBody: null,
            idempotencyKey: "notification:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    [Fact]
    public async Task Publisher_delivers_attributed_payload_to_matching_user_subscription()
    {
        using IHost host = BuildHost(enabled: true);
        IUserNotificationFeed feed = host.Services.GetRequiredService<IUserNotificationFeed>();
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();
        UserNotificationTarget target = UserNotificationTarget.User("tenant-a", "user-a");
        await using IUserNotificationSubscription subscription = feed.Subscribe(target);

        await publisher.PublishAsync(
            SampleModule.Name,
            target,
            new SampleNotificationPayload("ready"),
            new NotificationPublishOptions("Report ready", severity: NotificationSeverity.Success));

        UserNotificationMessage message = await ReadOneAsync(subscription);

        Assert.Equal("sample.event", message.Name);
        Assert.Equal(1, message.Version);
        Assert.Equal("tenant-a", message.ScopeId);
        Assert.Equal("user-a", message.UserId);
        Assert.Equal("Report ready", message.Title);
        Assert.Equal(NotificationSeverity.Success, message.Severity);
        Assert.Equal("ready", message.Payload.GetProperty("value").GetString());
        Assert.Equal([NotificationTags.Web], message.Tags);
        Assert.Equal(NotificationDeliveryPolicy.RespectPreferences, message.DeliveryPolicy);
    }

    [Fact]
    public async Task Disabled_notifications_bypass_delivery()
    {
        using IHost host = BuildHost(enabled: false);
        IUserNotificationFeed feed = host.Services.GetRequiredService<IUserNotificationFeed>();
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();
        UserNotificationTarget target = UserNotificationTarget.User("tenant-a", "user-a");
        await using IUserNotificationSubscription subscription = feed.Subscribe(target);

        await publisher.PublishAsync(
            SampleModule.Name,
            target,
            new SampleNotificationPayload("ignored"),
            new NotificationPublishOptions("Ignored"));

        await AssertNoMessageAsync(subscription);
    }

    [Fact]
    public async Task Disabled_notifications_still_call_history_writers()
    {
        RecordingHistoryWriter historyWriter = new();
        using IHost host = BuildHost(
            enabled: false,
            configureServices: services =>
            {
                services.AddSingleton(historyWriter);
                services.AddSingleton<IUserNotificationHistoryWriter>(
                    provider => provider.GetRequiredService<RecordingHistoryWriter>());
            });
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();

        await publisher.PublishAsync(
            SampleModule.Name,
            UserNotificationTarget.User("tenant-a", "user-a"),
            new SampleNotificationPayload("history"),
            new NotificationPublishOptions("History only"));

        UserNotificationMessage message = Assert.Single(historyWriter.Messages);
        Assert.Equal("History only", message.Title);
        Assert.Equal("history", message.Payload.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Slow_subscribers_keep_bounded_queue_and_drop_oldest_message()
    {
        using IHost host = BuildHost(enabled: true, subscriberQueueCapacity: 1);
        IUserNotificationFeed feed = host.Services.GetRequiredService<IUserNotificationFeed>();
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();
        UserNotificationTarget target = UserNotificationTarget.User("tenant-a", "user-a");
        await using IUserNotificationSubscription subscription = feed.Subscribe(target);

        await publisher.PublishAsync(
            SampleModule.Name,
            target,
            new SampleNotificationPayload("first"),
            new NotificationPublishOptions("First"));
        await publisher.PublishAsync(
            SampleModule.Name,
            target,
            new SampleNotificationPayload("second"),
            new NotificationPublishOptions("Second"));

        UserNotificationMessage message = await ReadOneAsync(subscription);

        Assert.Equal("Second", message.Title);
        Assert.Equal("second", message.Payload.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Sink_failures_fail_open_for_publisher()
    {
        using IHost host = BuildHost(
            enabled: true,
            configureServices: services => services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IUserNotificationSink, ThrowingNotificationSink>()));
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();

        await publisher.PublishAsync(
            SampleModule.Name,
            UserNotificationTarget.User("tenant-a", "user-a"),
            new SampleNotificationPayload("safe"),
            new NotificationPublishOptions("Safe"));
    }

    [Fact]
    public async Task Publisher_routes_only_to_sinks_supporting_requested_delivery_tags()
    {
        RecordingNotificationSink email = new("email-recorder", NotificationTags.Email);
        RecordingNotificationSink web = new("web-recorder", NotificationTags.Web);
        using IHost host = BuildHost(
            enabled: true,
            configureServices: services =>
            {
                services.AddSingleton<IUserNotificationSink>(email);
                services.AddSingleton<IUserNotificationSink>(web);
            });
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();

        await publisher.PublishAsync(
            SampleModule.Name,
            UserNotificationTarget.User("tenant-a", "user-a"),
            new SampleNotificationPayload("email-only"),
            new NotificationPublishOptions(
                "Email only",
                tags: [NotificationTags.Email, "domain:security"],
                deliveryPolicy: NotificationDeliveryPolicy.Mandatory));

        UserNotificationMessage delivered = Assert.Single(email.Messages);
        Assert.Empty(web.Messages);
        Assert.Contains(NotificationTags.Email, delivered.Tags);
        Assert.Contains("domain:security", delivered.Tags);
        Assert.Equal(NotificationDeliveryPolicy.Mandatory, delivered.DeliveryPolicy);
    }

    [Fact]
    public async Task Publisher_does_not_invoke_durable_only_adapters_inline()
    {
        RecordingNotificationSink durableEmail = new(
            "email-durable",
            NotificationTags.Email,
            NotificationSinkDeliveryMode.Durable);
        using IHost host = BuildHost(
            enabled: true,
            configureServices: services => services.AddSingleton<IUserNotificationSink>(durableEmail));
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();

        await publisher.PublishAsync(
            SampleModule.Name,
            UserNotificationTarget.User("tenant-a", "user-a"),
            new SampleNotificationPayload("durable"),
            new NotificationPublishOptions("Durable", tags: [NotificationTags.Email]));

        Assert.Empty(durableEmail.Messages);
    }

    [Fact]
    public async Task Publisher_fails_closed_when_delivery_policy_suppresses_a_best_effort_tag()
    {
        RecordingNotificationSink web = new("web-recorder", NotificationTags.Web);
        using IHost host = BuildHost(
            enabled: true,
            configureServices: services =>
            {
                services.AddSingleton<IUserNotificationSink>(web);
                services.AddSingleton<IUserNotificationDeliveryPolicyEvaluator>(new DenyTagPolicy(NotificationTags.Web));
            });
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();

        await publisher.PublishAsync(
            SampleModule.Name,
            UserNotificationTarget.User("tenant-a", "user-a"),
            new SampleNotificationPayload("suppressed"),
            new NotificationPublishOptions("Suppressed"));

        Assert.Empty(web.Messages);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("delivery:")]
    [InlineData(":email")]
    [InlineData("delivery:email:primary")]
    public void Notification_tags_require_normalized_namespaced_values(string tag)
    {
        Assert.Throws<ArgumentException>(() => NotificationTags.Normalize(tag));
    }

    [Fact]
    public void Notification_tags_are_normalized_deduplicated_and_bounded()
    {
        IReadOnlyList<string> tags = NotificationTags.Copy(
            ["delivery:Email", "domain:security", NotificationTags.Email]);

        Assert.Equal(["delivery:email", "domain:security"], tags);
        Assert.Throws<ArgumentException>(() => NotificationTags.Copy(
            Enumerable.Range(0, NotificationTags.MaxCount + 1).Select(index => $"domain:tag-{index}")));
    }

    [Fact]
    public void Delivery_results_validate_safe_codes_and_receipts()
    {
        NotificationSinkDeliveryResult delivered = NotificationSinkDeliveryResult.Delivered("provider-123");
        NotificationSinkDeliveryResult retry = NotificationSinkDeliveryResult.Retry("rate-limited");

        Assert.Equal(NotificationSinkDeliveryOutcome.Delivered, delivered.Outcome);
        Assert.Equal("provider-123", delivered.ProviderMessageId);
        Assert.Equal(NotificationSinkDeliveryOutcome.Retry, retry.Outcome);
        Assert.Equal("rate-limited", retry.Code);
        Assert.Throws<ArgumentException>(() => NotificationSinkDeliveryResult.Retry("Rate limited"));
    }

    [Fact]
    public async Task History_writer_failures_fail_open_for_live_delivery()
    {
        using IHost host = BuildHost(
            enabled: true,
            configureServices: services => services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IUserNotificationHistoryWriter, ThrowingHistoryWriter>()));
        IUserNotificationFeed feed = host.Services.GetRequiredService<IUserNotificationFeed>();
        IUserNotificationPublisher publisher = host.Services.GetRequiredService<IUserNotificationPublisher>();
        UserNotificationTarget target = UserNotificationTarget.User("tenant-a", "user-a");
        await using IUserNotificationSubscription subscription = feed.Subscribe(target);

        await publisher.PublishAsync(
            SampleModule.Name,
            target,
            new SampleNotificationPayload("safe"),
            new NotificationPublishOptions("Safe"));

        UserNotificationMessage message = await ReadOneAsync(subscription);
        Assert.Equal("safe", message.Payload.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Request_queue_flushes_enqueued_notifications_to_publisher()
    {
        using IHost host = BuildHost(enabled: true);
        using IServiceScope scope = host.Services.CreateScope();
        IUserNotificationFeed feed = scope.ServiceProvider.GetRequiredService<IUserNotificationFeed>();
        IUserNotificationRequestQueue queue = scope.ServiceProvider.GetRequiredService<IUserNotificationRequestQueue>();
        IUserNotificationRequestQueueFlusher flusher = scope.ServiceProvider.GetRequiredService<IUserNotificationRequestQueueFlusher>();
        UserNotificationTarget target = UserNotificationTarget.User("tenant-a", "user-a");
        await using IUserNotificationSubscription subscription = feed.Subscribe(target);

        await queue.EnqueueAsync(
            SampleModule.Name,
            target,
            new SampleNotificationPayload("queued"),
            new NotificationPublishOptions("Queued"));
        await flusher.FlushAsync(CancellationToken.None);

        UserNotificationMessage message = await ReadOneAsync(subscription);

        Assert.Equal("Queued", message.Title);
        Assert.Equal("queued", message.Payload.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Notification_requests_flush_after_successful_unit_of_work_commit()
    {
        List<string> order = [];
        RecordingUnitOfWork unitOfWork = new(order);
        RecordingNotificationRequestFlusher flusher = new(order);
        CommandUnitOfWorkBehavior<TestCommand, Unit> unitOfWorkBehavior = new([unitOfWork]);
        NotificationRequestCommandBehavior<TestCommand, Unit> notificationBehavior = new(flusher);

        Result<Unit> result = await notificationBehavior.HandleAsync(
            new TestCommand(),
            () => unitOfWorkBehavior.HandleAsync(
                new TestCommand(),
                () =>
                {
                    order.Add("handler");
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["handler", "commit", "notify"], order);
    }

    [Fact]
    public async Task Notification_requests_do_not_flush_for_failed_command_or_commit()
    {
        List<string> failedCommandOrder = [];
        NotificationRequestCommandBehavior<TestCommand, Unit> failedCommandBehavior = new(
            new RecordingNotificationRequestFlusher(failedCommandOrder));

        Result<Unit> failed = await failedCommandBehavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(Result.Failure<Unit>(new Error("Test.Failed", "Expected failure."))),
            CancellationToken.None);

        Assert.True(failed.IsFailure);
        Assert.Empty(failedCommandOrder);

        List<string> failedCommitOrder = [];
        RecordingUnitOfWork unitOfWork = new(failedCommitOrder, throwOnCommit: true);
        CommandUnitOfWorkBehavior<TestCommand, Unit> unitOfWorkBehavior = new([unitOfWork]);
        NotificationRequestCommandBehavior<TestCommand, Unit> failedCommitBehavior = new(
            new RecordingNotificationRequestFlusher(failedCommitOrder));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failedCommitBehavior.HandleAsync(
            new TestCommand(),
            () => unitOfWorkBehavior.HandleAsync(
                new TestCommand(),
                () =>
                {
                    failedCommitOrder.Add("handler");
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None),
            CancellationToken.None));

        Assert.Equal(["handler", "commit"], failedCommitOrder);
    }

    [Fact]
    public async Task Notification_request_bridge_registers_before_unit_of_work()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Enabled"] = "false",
            ["Tenancy:Enabled"] = "false",
            ["ApplicationIdentity:Namespace"] = "test-app"
        });
        builder.AddUserNotificationsCqrs();
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Type[] behaviorTypes = scope.ServiceProvider
            .GetServices<ICommandPipelineBehavior<TestCommand, Unit>>()
            .Select(behavior => behavior.GetType())
            .ToArray();

        Assert.Equal(
            [
                typeof(ValidationCommandBehavior<TestCommand, Unit>),
                typeof(LoggingCommandBehavior<TestCommand, Unit>),
                typeof(NotificationRequestCommandBehavior<TestCommand, Unit>),
                typeof(CommandUnitOfWorkBehavior<TestCommand, Unit>)
            ],
            behaviorTypes);
    }

    [Fact]
    public async Task Notification_infrastructure_does_not_register_cqrs_pipeline_behavior()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Notifications:Enabled"] = "false";
        builder.Configuration["ApplicationIdentity:Namespace"] = "test-app";
        builder.AddUserNotificationsInfrastructure();
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();

        Assert.Empty(provider.GetServices<ICommandPipelineBehavior<TestCommand, Unit>>());
    }

    [Fact]
    public async Task Notification_infrastructure_does_not_register_live_feed_runtime()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Notifications:Enabled"] = "true";
        builder.Configuration["ApplicationIdentity:Namespace"] = "test-app";
        builder.AddUserNotificationsInfrastructure();
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();

        Assert.Null(provider.GetService<IUserNotificationFeed>());
        Assert.DoesNotContain(
            provider.GetServices<IUserNotificationSink>(),
            sink => string.Equals(sink.ProviderName, "memory", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Notification_realtime_bridge_registers_live_feed_without_publisher()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Notifications:Enabled"] = "true";
        builder.Configuration["Notifications:SubscriberQueueCapacity"] = "1";
        builder.Configuration["ApplicationIdentity:Namespace"] = "test-app";
        builder.AddUserNotificationsRealtime();
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IUserNotificationFeed>());
        Assert.Contains(
            provider.GetServices<IUserNotificationSink>(),
            sink => string.Equals(sink.ProviderName, "memory", StringComparison.Ordinal));
        Assert.Null(provider.GetService<IUserNotificationPublisher>());
    }

    [Fact]
    public void Module_descriptor_reads_notification_metadata_from_payload_attributes()
    {
        ModuleNotificationDescriptor notification = SampleModule.Descriptor.GetUserNotifications().Single();

        Assert.Equal("sample.event", notification.Name);
        Assert.Equal(1, notification.Version);
        Assert.Equal("Sample user-facing notification.", notification.Description);
    }

    [Fact]
    public void Notification_severity_json_uses_stable_string_names()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        Assert.Equal("\"warning\"", JsonSerializer.Serialize(NotificationSeverity.Warning, options));
        Assert.Equal(
            NotificationSeverity.Warning,
            JsonSerializer.Deserialize<NotificationSeverity>("\"warning\"", options));
        Assert.Equal(
            NotificationSeverity.Warning,
            JsonSerializer.Deserialize<NotificationSeverity>("\"Warning\"", options));
    }

    [Fact]
    public void Notification_severity_json_rejects_numeric_or_unknown_values()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationSeverity>("3", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationSeverity>("\"unknown\"", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(NotificationSeverity.Unknown, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((NotificationSeverity)999, options));
    }

    [Fact]
    public void Notification_sse_item_kind_json_uses_stable_string_names()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        UserNotificationMessage message = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "sample",
            "sample.event",
            1,
            "tenant-a",
            "user-a",
            "Sample",
            null,
            NotificationSeverity.Info,
            new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero),
            JsonSerializer.SerializeToElement(new { value = "sample" }, options));

        string json = JsonSerializer.Serialize(NotificationSseItem.FromNotification(message), options);

        Assert.Contains("\"kind\":\"notification\"", json, StringComparison.Ordinal);
        Assert.Equal(
            NotificationSseItemKind.Heartbeat,
            JsonSerializer.Deserialize<NotificationSseItemKind>("\"heartbeat\"", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationSseItemKind>("2", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationSseItemKind>("\"unknown\"", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(NotificationSseItemKind.Unknown, options));
    }

    [Fact]
    public void User_notification_message_json_round_trips_delivery_metadata()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        UserNotificationMessage expected = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "sample",
            "sample.event",
            2,
            "tenant-a",
            "user-a",
            "Sample",
            "Body",
            NotificationSeverity.Warning,
            new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero),
            JsonSerializer.SerializeToElement(new { value = "sample" }, options),
            [NotificationTags.Email, "domain:security"],
            NotificationDeliveryPolicy.Mandatory);

        string json = JsonSerializer.Serialize(expected, options);
        UserNotificationMessage actual = JsonSerializer.Deserialize<UserNotificationMessage>(json, options)!;

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Module, actual.Module);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.ScopeId, actual.ScopeId);
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Body, actual.Body);
        Assert.Equal(expected.Severity, actual.Severity);
        Assert.Equal(expected.OccurredAtUtc, actual.OccurredAtUtc);
        Assert.Equal(expected.DeliveryPolicy, actual.DeliveryPolicy);
        Assert.Equal(expected.Tags, actual.Tags);
        Assert.Equal(expected.Payload.GetRawText(), actual.Payload.GetRawText());
    }

    [Theory]
    [InlineData("")]
    [InlineData("sample..event")]
    [InlineData("sample.-event")]
    [InlineData("sample event")]
    public void Notification_names_are_validated(string name)
    {
        Assert.Throws<ArgumentException>(() => NotificationNames.NormalizeName(name));
    }

    private static IHost BuildHost(
        bool enabled,
        int subscriberQueueCapacity = NotificationsOptions.DefaultSubscriberQueueCapacity,
        Action<IServiceCollection>? configureServices = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Enabled"] = enabled.ToString(),
            ["Notifications:SubscriberQueueCapacity"] = subscriberQueueCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Notifications:MaximumPayloadBytes"] = "32768",
            ["ApplicationIdentity:Namespace"] = "test-app"
        });
        configureServices?.Invoke(builder.Services);
        builder.AddUserNotificationsInfrastructure();
        builder.AddUserNotificationsRealtime();

        return builder.Build();
    }

    private static async Task<UserNotificationMessage> ReadOneAsync(IUserNotificationSubscription subscription)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        await using IAsyncEnumerator<UserNotificationMessage> messages =
            subscription.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);

        Assert.True(await messages.MoveNextAsync().ConfigureAwait(false));
        return messages.Current;
    }

    private static async Task AssertNoMessageAsync(IUserNotificationSubscription subscription)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromMilliseconds(150));
        IAsyncEnumerator<UserNotificationMessage> messages =
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

    private static class SampleModule
    {
        public const string Name = "sample";

        public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
            .Create(Name)
            .WithUserNotification<SampleNotificationPayload>()
            .Build();
    }

    [NotificationName("sample.event")]
    [NotificationVersion(1)]
    [NotificationDescription("Sample user-facing notification.")]
    private sealed record SampleNotificationPayload(string Value) : IUserNotificationPayload;

    private sealed class ThrowingNotificationSink : IUserNotificationSink
    {
        public string ProviderName => "throwing";
        public IReadOnlyCollection<string> DeliveryTags { get; } = [NotificationTags.Web];
        public NotificationSinkDeliveryMode DeliveryModes => NotificationSinkDeliveryMode.BestEffort;

        public ValueTask<NotificationSinkDeliveryResult> DeliverAsync(
            NotificationSinkDeliveryRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Sink failed.");
    }

    private sealed class RecordingNotificationSink(
        string providerName,
        string deliveryTag,
        NotificationSinkDeliveryMode deliveryModes = NotificationSinkDeliveryMode.BestEffort) : IUserNotificationSink
    {
        public string ProviderName { get; } = providerName;
        public IReadOnlyCollection<string> DeliveryTags { get; } = [deliveryTag];
        public NotificationSinkDeliveryMode DeliveryModes { get; } = deliveryModes;
        public List<UserNotificationMessage> Messages { get; } = [];

        public ValueTask<NotificationSinkDeliveryResult> DeliverAsync(
            NotificationSinkDeliveryRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Messages.Add(request.Message);
            return ValueTask.FromResult(NotificationSinkDeliveryResult.Delivered());
        }
    }

    private sealed class DenyTagPolicy(string deniedTag) : IUserNotificationDeliveryPolicyEvaluator
    {
        public ValueTask<bool> ShouldDeliverAsync(
            UserNotificationMessage message,
            string deliveryTag,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(!string.Equals(deniedTag, deliveryTag, StringComparison.Ordinal));
    }

    private sealed class RecordingHistoryWriter : IUserNotificationHistoryWriter
    {
        public List<UserNotificationMessage> Messages { get; } = [];

        public ValueTask SaveAsync(UserNotificationMessage message, CancellationToken cancellationToken = default)
        {
            this.Messages.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingHistoryWriter : IUserNotificationHistoryWriter
    {
        public ValueTask SaveAsync(UserNotificationMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("History failed.");
    }

    private sealed record TestCommand : ITransactionalCommand<Unit>;

    private sealed class RecordingUnitOfWork(List<string> order, bool throwOnCommit = false) : IUnitOfWork
    {
        public string ModuleName => "framework";

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            order.Add("commit");
            return throwOnCommit
                ? throw new InvalidOperationException("Commit failed.")
                : Task.CompletedTask;
        }
    }

    private sealed class RecordingNotificationRequestFlusher(List<string> order) : IUserNotificationRequestQueueFlusher
    {
        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            order.Add("notify");
            return ValueTask.CompletedTask;
        }
    }
}

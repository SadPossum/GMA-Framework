namespace Gma.Framework.Tests;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;
using Xunit;

[Trait("Category", "Unit")]
[Collection(MetricsTestGroupDefinition.Name)]
public sealed class NatsJetStreamConsumerServiceTests
{
    [Fact]
    public async Task Disabled_consumers_do_not_require_nats_connection()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["NatsConsumers:Enabled"] = "false";
        builder.AddNatsJetStreamConsumers();

        using IHost host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task Disabled_consumers_do_not_fail_when_logger_throws()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        NatsJetStreamConsumerService service = CreateService(
            provider,
            new IntegrationEventSubscriptionRegistry([]),
            new NatsConsumerOptions { Enabled = false });

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Enabled_consumers_without_subscriptions_do_not_fail_when_logger_throws()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        NatsJetStreamConsumerService service = CreateService(
            provider,
            new IntegrationEventSubscriptionRegistry([]),
            new NatsConsumerOptions { Enabled = true });

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Fetch_expiration_stays_inside_nats_client_limits(int pollIntervalMilliseconds)
    {
        NatsConsumerOptions options = new()
        {
            PollInterval = TimeSpan.FromMilliseconds(pollIntervalMilliseconds)
        };

        Assert.True(options.EffectiveFetchExpires > TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Effective_consumer_runtime_values_are_safe_when_validation_is_bypassed()
    {
        NatsConsumerOptions options = new()
        {
            FetchBatchSize = 0,
            PollInterval = TimeSpan.Zero,
            AckWait = TimeSpan.Zero,
            MaxDeliver = 0,
            HandlerTimeout = TimeSpan.Zero,
            NakDelay = TimeSpan.Zero
        };

        Assert.Equal(1, options.EffectiveFetchBatchSize);
        Assert.Equal(TimeSpan.FromSeconds(1), options.EffectivePollInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(1100), options.EffectiveFetchExpires);
        Assert.Equal(TimeSpan.FromSeconds(30), options.EffectiveAckWait);
        Assert.Equal(1, options.EffectiveMaxDeliver);
        Assert.Equal(TimeSpan.FromSeconds(30), options.EffectiveHandlerTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), options.EffectiveNakDelay);
    }

    [Fact]
    public void Consumer_options_derive_ack_progress_interval_from_ack_wait()
    {
        NatsConsumerOptions options = new() { AckWait = TimeSpan.FromSeconds(30) };

        Assert.Equal(TimeSpan.FromSeconds(10), options.EffectiveAckProgressInterval);
    }

    [Fact]
    public void Consumer_options_reject_ack_progress_interval_not_shorter_than_ack_wait()
    {
        NatsConsumerOptions options = new()
        {
            AckWait = TimeSpan.FromSeconds(10),
            AckProgressInterval = TimeSpan.FromSeconds(10),
        };

        Assert.True(new NatsConsumerOptionsValidator().Validate(null, options).Failed);
    }

    [Fact]
    public async Task Ack_progress_loop_extends_ownership_until_processing_stops()
    {
        using CancellationTokenSource stop = new();
        int acknowledgements = 0;

        await NatsJetStreamConsumerService.RunAckProgressAsync(
            _ =>
            {
                if (Interlocked.Increment(ref acknowledgements) == 3)
                {
                    stop.Cancel();
                }

                return ValueTask.CompletedTask;
            },
            TimeSpan.FromMilliseconds(10),
            _ => { },
            stop.Token);

        Assert.Equal(3, acknowledgements);
    }

    [Fact]
    public void Jetstream_options_require_finite_stream_limits()
    {
        NatsJetStreamOptions options = new()
        {
            MaxAge = TimeSpan.Zero,
            MaxBytes = 0,
            MaxMessages = 0,
        };

        ValidateOptionsResult result = new NatsJetStreamOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("MaxAge", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("MaxBytes", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("MaxMessages", StringComparison.Ordinal));
    }

    [Fact]
    public void Durable_name_is_deterministic_and_safe_for_nats()
    {
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, TestIntegrationEventHandler>(
                "ordering-module",
                "gma.catalog.item-created.v1",
                "catalog-item-created-projection");

        string durableName = NatsJetStreamConsumerService.CreateDurableName(
            "gma",
            "development",
            subscription);

        Assert.Equal("gma-development-ordering-module-catalog-item-created-projection", durableName);
        Assert.DoesNotContain(".", durableName);
        Assert.DoesNotContain("*", durableName);
        Assert.DoesNotContain(">", durableName);
        Assert.DoesNotContain("/", durableName);
        Assert.DoesNotContain("\\", durableName);
        Assert.DoesNotContain(" ", durableName);
    }

    [Theory]
    [InlineData("gma.prod", "development")]
    [InlineData("gma", "Dev Environment")]
    [InlineData("gma", "-development")]
    public void Durable_name_rejects_invalid_physical_segments(string durablePrefix, string environmentName)
    {
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, TestIntegrationEventHandler>(
                "ordering",
                "gma.catalog.item-created.v1",
                "catalog-item-created-projection");

        Assert.Throws<ArgumentException>(() =>
            NatsJetStreamConsumerService.CreateDurableName(
                durablePrefix,
                environmentName,
                subscription));
    }

    [Fact]
    public void Publisher_and_consumer_constructors_reject_invalid_stream_names()
    {
        NatsJetStreamOptions invalidOptions = new() { StreamName = "GMA.EVENTS" };
        ServiceCollection services = new();
        services.AddMetrics();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.True(new NatsJetStreamOptionsValidator().Validate(null, invalidOptions).Failed);
    }

    [Fact]
    public async Task Handler_invocation_preserves_synchronous_handler_exception()
    {
        ServiceCollection services = new();
        services.AddScoped<SynchronouslyThrowingIntegrationEventHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, SynchronouslyThrowingIntegrationEventHandler>(
                "ordering",
                "gma.catalog.item-created.v1",
                "sync-throwing-handler");
        TestIntegrationEvent integrationEvent = new(
            Guid.NewGuid(),
            "tenant-a",
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NatsJetStreamConsumerService.InvokeHandlerAsync(
                provider,
                subscription,
                integrationEvent,
                CancellationToken.None));

        Assert.Equal("handler failed directly", exception.Message);
    }

    [Fact]
    public async Task Handler_invocation_rejects_event_type_mismatch()
    {
        ServiceCollection services = new();
        services.AddScoped<TestIntegrationEventHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, TestIntegrationEventHandler>(
                "ordering",
                "gma.catalog.item-created.v1",
                "test-handler");
        OtherIntegrationEvent integrationEvent = new(
            Guid.NewGuid(),
            "tenant-a",
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NatsJetStreamConsumerService.InvokeHandlerAsync(
                provider,
                subscription,
                integrationEvent,
                CancellationToken.None));

        Assert.Contains(typeof(OtherIntegrationEvent).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(TestIntegrationEvent).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_invocation_rejects_null_handler_task()
    {
        ServiceCollection services = new();
        services.AddScoped<NullTaskIntegrationEventHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, NullTaskIntegrationEventHandler>(
                "ordering",
                "gma.catalog.item-created.v1",
                "null-task-handler");
        TestIntegrationEvent integrationEvent = new(
            Guid.NewGuid(),
            "tenant-a",
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NatsJetStreamConsumerService.InvokeHandlerAsync(
                provider,
                subscription,
                integrationEvent,
                CancellationToken.None));

        Assert.Contains("returned a null task", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Inbox_store_lookup_resolves_normalized_module_names()
    {
        ServiceCollection services = new();
        RecordingInboxStore ordering = new(" Ordering ");
        services.AddSingleton<IInboxStore>(ordering);
        using ServiceProvider provider = services.BuildServiceProvider();

        IInboxStore store = NatsJetStreamConsumerService.GetRequiredInboxStore(provider, "ordering");

        Assert.Same(ordering, store);
    }

    [Fact]
    public void Inbox_store_lookup_rejects_missing_or_duplicate_modules()
    {
        ServiceCollection missingServices = new();
        missingServices.AddSingleton<IInboxStore>(new RecordingInboxStore("auth"));
        using ServiceProvider missingProvider = missingServices.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            NatsJetStreamConsumerService.GetRequiredInboxStore(missingProvider, "ordering"));

        ServiceCollection duplicateServices = new();
        duplicateServices.AddSingleton<IInboxStore>(new RecordingInboxStore("ordering"));
        duplicateServices.AddSingleton<IInboxStore>(new RecordingInboxStore(" Ordering "));
        using ServiceProvider duplicateProvider = duplicateServices.BuildServiceProvider();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            NatsJetStreamConsumerService.GetRequiredInboxStore(duplicateProvider, "ordering"));

        Assert.Contains("2 inbox stores are registered for module 'ordering'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Inbox_store_lookup_rejects_invalid_store_module_name()
    {
        ServiceCollection services = new();
        services.AddSingleton<IInboxStore>(new RecordingInboxStore("ordering.module"));
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            NatsJetStreamConsumerService.GetRequiredInboxStore(provider, "ordering"));

        Assert.Contains("invalid module name", exception.Message, StringComparison.Ordinal);
    }

    private sealed record TestIntegrationEvent(
        Guid EventId,
        string Payload,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent
    {
        public string EventName => "test";
        public int Version => 1;
    }

    private sealed class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class SynchronouslyThrowingIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("handler failed directly");
    }

    private sealed record OtherIntegrationEvent(
        Guid EventId,
        string Payload,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent
    {
        public string EventName => "other";
        public int Version => 1;
    }

    private sealed class NullTaskIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            null!;
    }

    private sealed class RecordingInboxStore(string moduleName) : IInboxStore
    {
        public string ModuleName { get; } = moduleName;

        public Task<InboxProcessResult> ProcessAsync(
            InboxMessageRecord message,
            Func<CancellationToken, Task> handler,
            CancellationToken cancellationToken) =>
            Task.FromResult(InboxProcessResult.Processed());
    }

    private static NatsJetStreamConsumerService CreateService(
        ServiceProvider provider,
        IIntegrationEventSubscriptionRegistry registry,
        NatsConsumerOptions consumerOptions) =>
        new(
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            Options.Create(consumerOptions),
            Options.Create(new ApplicationIdentityOptions()),
            new TestHostEnvironment(),
            new InboxMetrics(
                provider.GetRequiredService<IMeterFactory>(),
                Options.Create(new ApplicationIdentityOptions())),
            new ThrowingLogger());

    private sealed class ThrowingLogger : ILogger<NatsJetStreamConsumerService>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Tests";
        public string ApplicationName { get; set; } = "Gma.Framework.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

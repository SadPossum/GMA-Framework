namespace Gma.Framework.Tests;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Observability.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
[Collection(MetricsTestGroupDefinition.Name)]
public sealed class OutboxPublisherServiceTests
{
    [Fact]
    public async Task Publish_failure_is_marked_failed_when_logger_throws()
    {
        DateTimeOffset now = new(2026, 7, 1, 15, 0, 0, TimeSpan.Zero);
        OutboxMessageRecord message = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "gma.auth.member-registered.v1",
            "Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent",
            1,
            "tenant-a",
            now,
            "{}");
        RecordingOutboxStore store = new(message);
        ThrowingEventBus eventBus = new();
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IOutboxStore>(store);
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxPublisherService service = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(now),
            new FixedIdGenerator(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff")),
            Options.Create(new OutboxOptions
            {
                BatchSize = 1,
                PollIntervalMilliseconds = 10_000,
                LockDurationMilliseconds = 1_000,
            }),
            new OutboxMetrics(
                provider.GetRequiredService<IMeterFactory>(),
                Options.Create(new ApplicationIdentityOptions())),
            new ThrowingLogger<OutboxPublisherService>());

        await service.StartAsync(CancellationToken.None);
        await store.WaitForFailureAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, eventBus.PublishAttempts);
        Assert.Equal(1, store.MarkFailedCalls);
        Assert.Equal(0, store.MarkProcessedCalls);
        Assert.Equal(message.Id, store.FailedMessageId);
        Assert.Equal("outbox-publish-failed:InvalidOperationException", store.FailedError);
        Assert.DoesNotContain(ThrowingEventBus.PersonalDataCanary, store.FailedError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_cancellation_is_marked_failed_when_host_is_not_stopping()
    {
        DateTimeOffset now = new(2026, 7, 1, 15, 0, 0, TimeSpan.Zero);
        OutboxMessageRecord message = new(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "gma.auth.member-registered.v1",
            "Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent",
            1,
            "tenant-a",
            now,
            "{}");
        RecordingOutboxStore store = new(message);
        CancelingEventBus eventBus = new();
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IOutboxStore>(store);
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxPublisherService service = CreateService(provider, now, new ThrowingLogger<OutboxPublisherService>());

        await service.StartAsync(CancellationToken.None);
        await store.WaitForFailureAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, eventBus.PublishAttempts);
        Assert.Equal(1, store.MarkFailedCalls);
        Assert.Equal(0, store.MarkProcessedCalls);
        Assert.Equal(message.Id, store.FailedMessageId);
        Assert.Equal("Outbox publish attempt was canceled before completion.", store.FailedError);
    }

    [Fact]
    public async Task Claim_failure_in_one_store_does_not_prevent_other_stores_from_publishing()
    {
        DateTimeOffset now = new(2026, 7, 1, 15, 0, 0, TimeSpan.Zero);
        OutboxMessageRecord message = new(
            Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"),
            "gma.catalog.item-created.v1",
            "Catalog.Contracts.IntegrationEvents.ItemCreatedIntegrationEvent",
            1,
            "tenant-a",
            now,
            "{}");
        ThrowingClaimOutboxStore failingStore = new("auth");
        RecordingOutboxStore healthyStore = new(message, "catalog");
        RecordingEventBus eventBus = new();
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IOutboxStore>(failingStore);
        services.AddSingleton<IOutboxStore>(healthyStore);
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxPublisherService service = CreateService(provider, now, new ThrowingLogger<OutboxPublisherService>());

        await service.StartAsync(CancellationToken.None);
        await healthyStore.WaitForProcessedAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, failingStore.ClaimCalls);
        Assert.Equal(1, eventBus.PublishAttempts);
        Assert.Equal(1, healthyStore.MarkProcessedCalls);
        Assert.Equal(0, healthyStore.MarkFailedCalls);
    }

    [Fact]
    public async Task Metrics_failure_does_not_prevent_successful_publish_marking()
    {
        DateTimeOffset now = new(2026, 7, 1, 15, 0, 0, TimeSpan.Zero);
        OutboxMessageRecord message = new(
            Guid.Parse("bbbbbbbb-7777-8888-9999-aaaaaaaaaaaa"),
            "gma.catalog.item-created.v1",
            "Catalog.Contracts.IntegrationEvents.ItemCreatedIntegrationEvent",
            1,
            "tenant-a",
            now,
            "{}");
        RecordingOutboxStore store = new(message, "catalog");
        RecordingEventBus eventBus = new();
        using MeterListener listener = CreateThrowingMessagingMeterListener();
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IOutboxStore>(store);
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxPublisherService service = CreateService(provider, now, new ThrowingLogger<OutboxPublisherService>());

        await service.StartAsync(CancellationToken.None);
        await store.WaitForProcessedAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, eventBus.PublishAttempts);
        Assert.Equal(1, store.MarkProcessedCalls);
        Assert.Equal(0, store.MarkFailedCalls);
    }

    [Fact]
    public async Task Metrics_failure_does_not_prevent_failed_publish_marking()
    {
        DateTimeOffset now = new(2026, 7, 1, 15, 0, 0, TimeSpan.Zero);
        OutboxMessageRecord message = new(
            Guid.Parse("cccccccc-7777-8888-9999-aaaaaaaaaaaa"),
            "gma.auth.member-registered.v1",
            "Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent",
            1,
            "tenant-a",
            now,
            "{}");
        RecordingOutboxStore store = new(message);
        ThrowingEventBus eventBus = new();
        using MeterListener listener = CreateThrowingMessagingMeterListener();
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IOutboxStore>(store);
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxPublisherService service = CreateService(provider, now, new ThrowingLogger<OutboxPublisherService>());

        await service.StartAsync(CancellationToken.None);
        await store.WaitForFailureAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, eventBus.PublishAttempts);
        Assert.Equal(1, store.MarkFailedCalls);
        Assert.Equal(0, store.MarkProcessedCalls);
        Assert.Equal("outbox-publish-failed:InvalidOperationException", store.FailedError);
        Assert.DoesNotContain(ThrowingEventBus.PersonalDataCanary, store.FailedError, StringComparison.Ordinal);
    }

    [Fact]
    public void Outbox_store_registration_normalizes_module_names()
    {
        OutboxMessageRecord message = CreateMessageRecord();
        RecordingOutboxStore store = new(message, " Auth ");
        ServiceCollection services = new();
        services.AddSingleton<IOutboxStore>(store);
        using ServiceProvider provider = services.BuildServiceProvider();

        OutboxPublisherService.OutboxStoreRegistration registration =
            Assert.Single(OutboxPublisherService.GetRequiredOutboxStores(provider));

        Assert.Equal("auth", registration.ModuleName);
        Assert.Same(store, registration.Store);
    }

    [Fact]
    public void Outbox_store_registration_rejects_duplicate_module_names()
    {
        OutboxMessageRecord message = CreateMessageRecord();
        ServiceCollection services = new();
        services.AddSingleton<IOutboxStore>(new RecordingOutboxStore(message, "auth"));
        services.AddSingleton<IOutboxStore>(new RecordingOutboxStore(message, " Auth "));
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            OutboxPublisherService.GetRequiredOutboxStores(provider));

        Assert.Contains("2 outbox stores are registered for module 'auth'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Outbox_store_registration_rejects_invalid_module_names()
    {
        OutboxMessageRecord message = CreateMessageRecord();
        ServiceCollection services = new();
        services.AddSingleton<IOutboxStore>(new RecordingOutboxStore(message, "auth.module"));
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            OutboxPublisherService.GetRequiredOutboxStores(provider));

        Assert.Contains("invalid module name", exception.Message, StringComparison.Ordinal);
    }

    private static OutboxPublisherService CreateService(
        ServiceProvider provider,
        DateTimeOffset now,
        ILogger<OutboxPublisherService> logger) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(now),
            new FixedIdGenerator(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff")),
            Options.Create(new OutboxOptions
            {
                BatchSize = 1,
                PollIntervalMilliseconds = 10_000,
                LockDurationMilliseconds = 1_000,
            }),
            new OutboxMetrics(
                provider.GetRequiredService<IMeterFactory>(),
                Options.Create(new ApplicationIdentityOptions())),
            logger);

    private static MeterListener CreateThrowingMessagingMeterListener()
    {
        MeterListener listener = new()
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == ObservabilityMeterNames.Messaging)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
            throw new InvalidOperationException("Metrics listener unavailable."));
        listener.SetMeasurementEventCallback<double>((_, _, _, _) =>
            throw new InvalidOperationException("Metrics listener unavailable."));
        listener.Start();

        return listener;
    }

    private static OutboxMessageRecord CreateMessageRecord() =>
        new(
            Guid.Parse("dddddddd-7777-8888-9999-aaaaaaaaaaaa"),
            "gma.auth.member-registered.v1",
            "Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent",
            1,
            "tenant-a",
            new DateTimeOffset(2026, 7, 1, 15, 0, 0, TimeSpan.Zero),
            "{}");

    private sealed class RecordingOutboxStore(OutboxMessageRecord message, string moduleName = "auth") : IOutboxStore
    {
        private readonly TaskCompletionSource failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource processed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int claimed;

        public string ModuleName => moduleName;
        public int MarkFailedCalls { get; private set; }
        public int MarkProcessedCalls { get; private set; }
        public Guid? FailedMessageId { get; private set; }
        public string? FailedError { get; private set; }

        public Task<IReadOnlyList<OutboxMessageRecord>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan lockDuration,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OutboxMessageRecord> messages = Interlocked.Exchange(ref this.claimed, 1) == 0
                ? [message]
                : [];

            return Task.FromResult(messages);
        }

        public Task MarkProcessedAsync(Guid id, string workerId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            this.MarkProcessedCalls++;
            this.processed.SetResult();
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid id, string workerId, string error, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            this.MarkFailedCalls++;
            this.FailedMessageId = id;
            this.FailedError = error;
            this.failed.SetResult();
            return Task.CompletedTask;
        }

        public async Task WaitForFailureAsync(TimeSpan timeout)
        {
            Task completed = await Task.WhenAny(this.failed.Task, Task.Delay(timeout)).ConfigureAwait(false);
            Assert.Same(this.failed.Task, completed);
        }

        public async Task WaitForProcessedAsync(TimeSpan timeout)
        {
            Task completed = await Task.WhenAny(this.processed.Task, Task.Delay(timeout)).ConfigureAwait(false);
            Assert.Same(this.processed.Task, completed);
        }
    }

    private sealed class ThrowingEventBus : IEventBus
    {
        public const string PersonalDataCanary = "guest.aprokudanov@example.test";

        public int PublishAttempts { get; private set; }

        public Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken)
        {
            this.PublishAttempts++;
            throw new InvalidOperationException(PersonalDataCanary);
        }
    }

    private sealed class CancelingEventBus : IEventBus
    {
        public int PublishAttempts { get; private set; }

        public Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken)
        {
            this.PublishAttempts++;
            throw new OperationCanceledException();
        }
    }

    private sealed class RecordingEventBus : IEventBus
    {
        public int PublishAttempts { get; private set; }

        public Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken)
        {
            this.PublishAttempts++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingClaimOutboxStore(string moduleName) : IOutboxStore
    {
        public string ModuleName => moduleName;
        public int ClaimCalls { get; private set; }

        public Task<IReadOnlyList<OutboxMessageRecord>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan lockDuration,
            CancellationToken cancellationToken)
        {
            this.ClaimCalls++;
            throw new InvalidOperationException("Database unavailable.");
        }

        public Task MarkProcessedAsync(Guid id, string workerId, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task MarkFailedAsync(Guid id, string workerId, string error, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FixedIdGenerator(Guid id) : IIdGenerator
    {
        public Guid NewId() => id;
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            throw new InvalidOperationException("Logger scope unavailable.");

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }
}

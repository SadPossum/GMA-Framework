namespace Gma.Framework.Messaging.Nats;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;

internal sealed class NatsJetStreamConsumerService(
    IServiceProvider services,
    IServiceScopeFactory scopeFactory,
    IIntegrationEventSubscriptionRegistry subscriptions,
    IOptions<NatsConsumerOptions> options,
    IOptions<NatsJetStreamOptions> jetStreamOptions,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IHostEnvironment environment,
    InboxMetrics metrics,
    ILogger<NatsJetStreamConsumerService> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim streamSetupLock = new(1, 1);
    private readonly string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
    private readonly string streamName = jetStreamOptions.Value.EffectiveStreamName(applicationIdentity.Value.EffectiveNamespace);
    private volatile bool streamReady;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            this.LogConsumersDisabled();
            return;
        }

        if (subscriptions.Subscriptions.Count == 0)
        {
            this.LogNoSubscriptions();
            return;
        }

        this.ValidateInboxStores();

        INatsConnection connection = services.GetRequiredService<INatsConnection>();
        NatsJSContext jetStream = new(connection);
        await this.EnsureStreamAsync(jetStream, stoppingToken).ConfigureAwait(false);

        Task[] workers = subscriptions.Subscriptions
            .Select(subscription => this.RunSubscriptionAsync(jetStream, subscription, stoppingToken))
            .ToArray();

        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task RunSubscriptionAsync(
        NatsJSContext jetStream,
        IntegrationEventSubscription subscription,
        CancellationToken stoppingToken)
    {
        string durableName = this.GetDurableName(subscription);
        string subject = subscription.CreateSubject(this.applicationNamespace);
        ConsumerConfig consumerConfig = new(durableName)
        {
            FilterSubject = subject,
            AckWait = options.Value.EffectiveAckWait,
            MaxDeliver = options.Value.EffectiveMaxDeliver,
            MaxAckPending = options.Value.EffectiveFetchBatchSize
        };

        INatsJSConsumer consumer = await jetStream
            .CreateOrUpdateConsumerAsync(this.streamName, consumerConfig, stoppingToken)
            .ConfigureAwait(false);

        this.LogConsumerStarted(durableName, subject);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                NatsJSFetchOpts fetchOptions = new()
                {
                    MaxMsgs = options.Value.EffectiveFetchBatchSize,
                    Expires = options.Value.EffectiveFetchExpires
                };

                await foreach (INatsJSMsg<string> message in consumer.FetchAsync(
                                   fetchOptions,
                                   NatsDefaultSerializer<string>.Default,
                                   stoppingToken)
                               .ConfigureAwait(false))
                {
                    await this.HandleMessageAsync(message, subscription, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                this.LogPollingFailure(durableName, subject, exception);
                await Task.Delay(options.Value.EffectivePollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleMessageAsync(
        INatsJSMsg<string> message,
        IntegrationEventSubscription subscription,
        CancellationToken stoppingToken)
    {
        string subject = subscription.CreateSubject(this.applicationNamespace);
        IIntegrationEvent? integrationEvent;
        try
        {
            if (string.IsNullOrWhiteSpace(message.Data))
            {
                await message.AckTerminateAsync(
                        new AckOpts { TerminateReason = "empty-payload" },
                        stoppingToken)
                    .ConfigureAwait(false);
                return;
            }

            integrationEvent = JsonSerializer.Deserialize(
                message.Data,
                subscription.EventType,
                JsonOptions) as IIntegrationEvent;
        }
        catch (JsonException exception)
        {
            this.LogDeserializationFailure(subject, subscription.EventType.FullName, exception);
            await message.AckTerminateAsync(
                    new AckOpts { TerminateReason = "deserialization-failed" },
                    stoppingToken)
                .ConfigureAwait(false);
            return;
        }

        if (integrationEvent is null)
        {
            this.LogDeserializationNull(subject, subscription.EventType.FullName);
            await message.AckTerminateAsync(
                    new AckOpts { TerminateReason = "deserialization-returned-null" },
                    stoppingToken)
                .ConfigureAwait(false);
            return;
        }

        if (IntegrationEventMetadata.TryGetInvalidReason(integrationEvent, out string invalidReason))
        {
            this.LogInvalidEventMetadata(subject, subscription.EventType.FullName, invalidReason);
            await message.AckTerminateAsync(
                    new AckOpts { TerminateReason = invalidReason },
                    stoppingToken)
                .ConfigureAwait(false);
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        PrepareProcessingContext(scope.ServiceProvider, subscription, integrationEvent);
        string? scopeId = ResolveScopeId(scope.ServiceProvider, integrationEvent);

        IInboxStore inboxStore = GetRequiredInboxStore(scope.ServiceProvider, subscription.ConsumerModule);
        InboxMessageRecord inboxMessage = new(
            integrationEvent.EventId,
            subscription.HandlerName,
            subject,
            integrationEvent.EventName,
            integrationEvent.Version,
            scopeId,
            integrationEvent.OccurredAtUtc);

        using CancellationTokenSource handlerTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        handlerTimeout.CancelAfter(options.Value.EffectiveHandlerTimeout);
        long startedAt = Stopwatch.GetTimestamp();

        InboxProcessResult result = await inboxStore
            .ProcessAsync(
                inboxMessage,
                _ => InvokeHandlerAsync(scope.ServiceProvider, subscription, integrationEvent, handlerTimeout.Token),
                stoppingToken)
            .ConfigureAwait(false);
        this.TryRecordInboxProcessed(subscription, result.Status, Stopwatch.GetElapsedTime(startedAt));

        if (result.Status is InboxProcessStatus.Processed or InboxProcessStatus.Duplicate)
        {
            await message.AckAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
            return;
        }

        this.LogProcessingFailure(
            integrationEvent.EventId,
            subscription.ConsumerModule,
            subscription.HandlerName,
            result.Error);

        await message.NakAsync(new AckOpts { NakDelay = options.Value.EffectiveNakDelay }, stoppingToken)
            .ConfigureAwait(false);
    }

    internal static async Task InvokeHandlerAsync(
        IServiceProvider serviceProvider,
        IntegrationEventSubscription subscription,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await IntegrationEventHandlerInvoker
            .InvokeAsync(serviceProvider, subscription, integrationEvent, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void PrepareProcessingContext(
        IServiceProvider serviceProvider,
        IntegrationEventSubscription subscription,
        IIntegrationEvent integrationEvent)
    {
        foreach (IIntegrationEventProcessingContextContributor contributor in serviceProvider
                     .GetServices<IIntegrationEventProcessingContextContributor>())
        {
            contributor.Prepare(subscription, integrationEvent);
        }
    }

    private static string? ResolveScopeId(IServiceProvider serviceProvider, IIntegrationEvent integrationEvent)
    {
        string? resolved = null;
        foreach (IIntegrationEventScopeResolver resolver in serviceProvider.GetServices<IIntegrationEventScopeResolver>())
        {
            string? candidate = MessageScopeIds.NormalizeOptional(
                resolver.ResolveScopeId(integrationEvent),
                nameof(IIntegrationEventScopeResolver.ResolveScopeId));
            if (candidate is null)
            {
                continue;
            }

            if (resolved is not null &&
                !string.Equals(resolved, candidate, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Integration event '{integrationEvent.EventName}' resolved multiple different message scopes.");
            }

            resolved = candidate;
        }

        return resolved;
    }

    private void TryRecordInboxProcessed(
        IntegrationEventSubscription subscription,
        InboxProcessStatus status,
        TimeSpan elapsed)
    {
        try
        {
            metrics.RecordProcessed(
                subscription.ConsumerModule,
                subscription.HandlerName,
                subscription.CreateSubject(this.applicationNamespace),
                status,
                elapsed);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not affect message acknowledgement.
        }
    }

    private void ValidateInboxStores()
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        foreach (IntegrationEventSubscription subscription in subscriptions.Subscriptions)
        {
            _ = GetRequiredInboxStore(scope.ServiceProvider, subscription.ConsumerModule);
        }
    }

    internal static IInboxStore GetRequiredInboxStore(IServiceProvider serviceProvider, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        string normalized = IntegrationEventNaming.NormalizeModuleName(moduleName);
        InboxStoreRegistration[] registrations = serviceProvider
            .GetServices<IInboxStore>()
            .Select(CreateInboxStoreRegistration)
            .ToArray();

        IGrouping<string, InboxStoreRegistration>? duplicate = registrations
            .GroupBy(registration => registration.ModuleName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} inbox stores are registered for module '{duplicate.Key}'.");
        }

        Dictionary<string, IInboxStore> storesByModule = registrations.ToDictionary(
            registration => registration.ModuleName,
            registration => registration.Store,
            StringComparer.Ordinal);

        return storesByModule.TryGetValue(normalized, out IInboxStore? store)
            ? store
            : throw new InvalidOperationException(
                $"No inbox store is registered for module '{normalized}'. Register a module-owned IInboxStore before enabling NATS consumers.");
    }

    private static InboxStoreRegistration CreateInboxStoreRegistration(IInboxStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        try
        {
            return new(
                IntegrationEventNaming.NormalizeModuleName(store.ModuleName, nameof(IInboxStore.ModuleName)),
                store);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Inbox store '{store.GetType().FullName}' has an invalid module name.",
                exception);
        }
    }

    private sealed record InboxStoreRegistration(string ModuleName, IInboxStore Store);

    private async Task EnsureStreamAsync(NatsJSContext jetStream, CancellationToken cancellationToken)
    {
        if (this.streamReady)
        {
            return;
        }

        await this.streamSetupLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (this.streamReady)
            {
                return;
            }

            await jetStream.CreateStreamAsync(
                    new StreamConfig(this.streamName, [NatsJetStreamOptions.CreateSubjectWildcard(this.applicationNamespace)]),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            this.streamReady = true;
        }
        catch (NatsJSApiException exception) when (IsAlreadyExists(exception))
        {
            this.LogStreamAlreadyExists(exception);
            this.streamReady = true;
        }
        finally
        {
            this.streamSetupLock.Release();
        }
    }

    private string GetDurableName(IntegrationEventSubscription subscription)
    {
        return CreateDurableName(options.Value.EffectiveDurablePrefix(this.applicationNamespace), environment.EnvironmentName, subscription);
    }

    internal static string CreateDurableName(
        string durablePrefix,
        string environmentName,
        IntegrationEventSubscription subscription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durablePrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentNullException.ThrowIfNull(subscription);

        return NatsConsumerDurableName.Create(durablePrefix, environmentName, subscription);
    }

    private static bool IsAlreadyExists(NatsJSApiException exception)
    {
        string description = exception.Error.Description ?? string.Empty;

        return description.Contains("already", StringComparison.OrdinalIgnoreCase) &&
               (description.Contains("exist", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("in use", StringComparison.OrdinalIgnoreCase));
    }

    private void LogConsumersDisabled()
    {
        try
        {
            logger.LogInformation("NATS JetStream consumers are disabled.");
        }
        catch (Exception)
        {
        }
    }

    private void LogNoSubscriptions()
    {
        try
        {
            logger.LogInformation("NATS JetStream consumers are enabled, but no integration event subscriptions are registered.");
        }
        catch (Exception)
        {
        }
    }

    private void LogConsumerStarted(string durableName, string subject)
    {
        try
        {
            logger.LogInformation(
                "Started NATS JetStream consumer {DurableName} for {Subject}",
                durableName,
                subject);
        }
        catch (Exception)
        {
        }
    }

    private void LogPollingFailure(string durableName, string subject, Exception exception)
    {
        try
        {
            logger.LogError(
                exception,
                "NATS JetStream consumer {DurableName} failed while polling {Subject}",
                durableName,
                subject);
        }
        catch (Exception)
        {
        }
    }

    private void LogDeserializationFailure(string subject, string? eventType, JsonException exception)
    {
        try
        {
            logger.LogError(
                exception,
                "Failed to deserialize NATS message on {Subject} as {EventType}",
                subject,
                eventType);
        }
        catch (Exception)
        {
        }
    }

    private void LogDeserializationNull(string subject, string? eventType)
    {
        try
        {
            logger.LogError(
                "Failed to deserialize NATS message on {Subject} as {EventType}",
                subject,
                eventType);
        }
        catch (Exception)
        {
        }
    }

    private void LogInvalidEventMetadata(string subject, string? eventType, string reason)
    {
        try
        {
            logger.LogError(
                "Rejected NATS message on {Subject} as {EventType} because integration event metadata is invalid: {Reason}",
                subject,
                eventType,
                reason);
        }
        catch (Exception)
        {
        }
    }

    private void LogProcessingFailure(Guid eventId, string consumerModule, string handlerName, string? error)
    {
        try
        {
            logger.LogWarning(
                "NATS message {EventId} for handler {ConsumerModule}.{HandlerName} failed: {Error}",
                eventId,
                consumerModule,
                handlerName,
                error);
        }
        catch (Exception)
        {
        }
    }

    private void LogStreamAlreadyExists(NatsJSApiException exception)
    {
        try
        {
            logger.LogDebug(exception, "NATS stream {StreamName} already exists.", this.streamName);
        }
        catch (Exception)
        {
        }
    }

    public override void Dispose()
    {
        this.streamSetupLock.Dispose();
        base.Dispose();
    }
}

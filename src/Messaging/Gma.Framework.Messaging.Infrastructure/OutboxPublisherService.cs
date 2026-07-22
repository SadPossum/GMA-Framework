namespace Gma.Framework.Messaging.Infrastructure;

using System.Diagnostics;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime.Failures;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Runtime.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    ISystemClock clock,
    IIdGenerator idGenerator,
    IOptions<OutboxOptions> options,
    OutboxMetrics metrics,
    ILogger<OutboxPublisherService> logger)
    : BackgroundService
{
    private const string PublishCanceledError = "Outbox publish attempt was canceled before completion.";

    private readonly string workerId = WorkerIds.Create(Environment.MachineName, idGenerator.NewId());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(options.Value.EffectivePollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await this.PublishPendingAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IEventBus eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        OutboxStoreRegistration[] stores = GetRequiredOutboxStores(scope.ServiceProvider);

        foreach (OutboxStoreRegistration registration in stores)
        {
            IOutboxStore store = registration.Store;
            string moduleName = registration.ModuleName;
            IReadOnlyList<OutboxMessageRecord> messages;
            try
            {
                DateTimeOffset nowUtc = clock.UtcNow;
                messages = await store
                    .ClaimPendingAsync(
                        options.Value.EffectiveBatchSize,
                        this.workerId,
                        nowUtc,
                        options.Value.EffectiveLockDuration,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                this.LogStoreFailure(moduleName, exception);
                continue;
            }

            this.TryRecordClaimed(moduleName, messages.Count);

            foreach (OutboxMessageRecord message in messages)
            {
                long startedAt = Stopwatch.GetTimestamp();
                Dictionary<string, object?> scopeProperties = new()
                {
                    [ObservabilityLogPropertyNames.Module] = moduleName,
                    [ObservabilityLogPropertyNames.Subject] = message.Subject,
                    [ObservabilityLogPropertyNames.MessageScoped] = !string.IsNullOrWhiteSpace(message.ScopeId),
                    [ObservabilityLogPropertyNames.TraceId] = Activity.Current?.TraceId.ToString(),
                };

                IDisposable? logScope = this.BeginLogScope(scopeProperties);

                try
                {
                    await eventBus.PublishAsync(message, cancellationToken).ConfigureAwait(false);
                    await store.MarkProcessedAsync(message.Id, this.workerId, clock.UtcNow, cancellationToken)
                        .ConfigureAwait(false);
                    this.TryRecordPublished(moduleName, message.Subject, Stopwatch.GetElapsedTime(startedAt));
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    string error = GetFailureMessage(exception);
                    this.TryRecordFailed(moduleName, message.Subject, Stopwatch.GetElapsedTime(startedAt));
                    this.LogPublishFailure(moduleName, exception);
                    await this.TryMarkFailedAsync(store, moduleName, message.Id, error, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    DisposeLogScope(logScope);
                }
            }
        }
    }

    internal static OutboxStoreRegistration[] GetRequiredOutboxStores(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        OutboxStoreRegistration[] registrations = serviceProvider
            .GetServices<IOutboxStore>()
            .Select(CreateOutboxStoreRegistration)
            .ToArray();

        IGrouping<string, OutboxStoreRegistration>? duplicate = registrations
            .GroupBy(registration => registration.ModuleName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} outbox stores are registered for module '{duplicate.Key}'.");
        }

        return registrations;
    }

    private void TryRecordClaimed(string moduleName, int count)
    {
        try
        {
            metrics.RecordClaimed(moduleName, count);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not stop publishing.
        }
    }

    private void TryRecordPublished(string moduleName, string subject, TimeSpan elapsed)
    {
        try
        {
            metrics.RecordPublished(moduleName, subject, elapsed);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not stop state transitions.
        }
    }

    private void TryRecordFailed(string moduleName, string subject, TimeSpan elapsed)
    {
        try
        {
            metrics.RecordFailed(moduleName, subject, elapsed);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not stop retry bookkeeping.
        }
    }

    private async Task TryMarkFailedAsync(
        IOutboxStore store,
        string moduleName,
        Guid messageId,
        string error,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.MarkFailedAsync(messageId, this.workerId, error, clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            this.LogMarkFailedFailure(moduleName, exception);
        }
    }

    private static string GetFailureMessage(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return PublishCanceledError;
        }

        return RuntimeFailureDescriptions.FromException("outbox-publish-failed", exception);
    }

    private IDisposable? BeginLogScope(Dictionary<string, object?> scopeProperties)
    {
        try
        {
            return logger.BeginScope(scopeProperties);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void DisposeLogScope(IDisposable? logScope)
    {
        try
        {
            logScope?.Dispose();
        }
        catch (Exception)
        {
            // Logging scopes are observability only; disposal failures must not affect outbox state transitions.
        }
    }

    private void LogPublishFailure(string moduleName, Exception exception)
    {
        try
        {
            logger.LogError(
                "Failed to publish an outbox message from {ModuleName} with {ExceptionType}",
                moduleName,
                exception.GetType().Name);
        }
        catch (Exception)
        {
            // Publish failure bookkeeping must continue even if logging is unavailable.
        }
    }

    private void LogStoreFailure(string moduleName, Exception exception)
    {
        try
        {
            logger.LogError(
                "Failed to claim outbox messages from {ModuleName} with {ExceptionType}",
                moduleName,
                exception.GetType().Name);
        }
        catch (Exception)
        {
            // A single module store failure must not stop other module stores from publishing.
        }
    }

    private void LogMarkFailedFailure(string moduleName, Exception exception)
    {
        try
        {
            logger.LogError(
                "Failed to mark an outbox message from {ModuleName} as failed with {ExceptionType}",
                moduleName,
                exception.GetType().Name);
        }
        catch (Exception)
        {
            // The message remains locked until reclaim; logging failures should not stop the publisher loop.
        }
    }

    private static OutboxStoreRegistration CreateOutboxStoreRegistration(IOutboxStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        try
        {
            return new(
                IntegrationEventNaming.NormalizeModuleName(store.ModuleName, nameof(IOutboxStore.ModuleName)),
                store);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Outbox store '{store.GetType().FullName}' has an invalid module name.",
                exception);
        }
    }

    internal sealed record OutboxStoreRegistration(string ModuleName, IOutboxStore Store);
}

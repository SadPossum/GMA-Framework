namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class MessageJournalCleanupService(
    IServiceScopeFactory scopeFactory,
    ISystemClock clock,
    IOptions<MessageJournalCleanupOptions> options,
    MessageJournalMetrics metrics,
    ILogger<MessageJournalCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MessageJournalCleanupOptions currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            logger.LogInformation("Message journal cleanup is disabled.");
            return;
        }

        using PeriodicTimer timer = new(currentOptions.CleanupInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            await this.CleanupOnceAsync(currentOptions, stoppingToken).ConfigureAwait(false);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal async Task CleanupOnceAsync(
        MessageJournalCleanupOptions currentOptions,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();

        if (currentOptions.CleanupProcessedOutbox)
        {
            DateTimeOffset cutoff = clock.UtcNow.Subtract(currentOptions.ProcessedOutboxRetention);
            foreach (IOutboxCleanupStore store in GetOutboxCleanupStores(scope.ServiceProvider))
            {
                await this.CleanupStoreAsync(
                        store.ModuleName,
                        "outbox",
                        (batchSize, token) => store.DeleteProcessedBeforeAsync(cutoff, batchSize, token),
                        currentOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (currentOptions.CleanupProcessedInbox)
        {
            DateTimeOffset cutoff = clock.UtcNow.Subtract(currentOptions.ProcessedInboxRetention);
            foreach (IInboxCleanupStore store in GetInboxCleanupStores(scope.ServiceProvider))
            {
                await this.CleanupStoreAsync(
                        store.ModuleName,
                        "inbox",
                        (batchSize, token) => store.DeleteProcessedBeforeAsync(cutoff, batchSize, token),
                        currentOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task CleanupStoreAsync(
        string moduleName,
        string journal,
        Func<int, CancellationToken, Task<int>> deleteBatch,
        MessageJournalCleanupOptions currentOptions,
        CancellationToken cancellationToken)
    {
        int deletedTotal = 0;
        try
        {
            for (int batch = 0; batch < currentOptions.MaxBatchesPerStorePerCycle; batch++)
            {
                int deleted = await deleteBatch(currentOptions.BatchSize, cancellationToken).ConfigureAwait(false);
                if (deleted < 0 || deleted > currentOptions.BatchSize)
                {
                    throw new InvalidOperationException(
                        $"Message journal cleanup store '{moduleName}' returned invalid batch count {deleted}.");
                }

                deletedTotal += deleted;
                if (deleted < currentOptions.BatchSize)
                {
                    break;
                }
            }

            if (deletedTotal > 0)
            {
                this.TryRecordDeleted(moduleName, journal, deletedTotal);
                logger.LogInformation(
                    "Deleted {DeletedCount} processed {Journal} messages for module {ModuleName}.",
                    deletedTotal,
                    journal,
                    moduleName);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this.TryRecordDeleted(moduleName, journal, deletedTotal);
            logger.LogError(
                exception,
                "Failed to clean processed {Journal} messages for module {ModuleName}; other stores will continue.",
                journal,
                moduleName);
        }
    }

    private static IOutboxCleanupStore[] GetOutboxCleanupStores(IServiceProvider serviceProvider) =>
        GetUniqueStores(
            serviceProvider.GetServices<IOutboxStore>().OfType<IOutboxCleanupStore>(),
            store => store.ModuleName,
            "outbox");

    private static IInboxCleanupStore[] GetInboxCleanupStores(IServiceProvider serviceProvider) =>
        GetUniqueStores(
            serviceProvider.GetServices<IInboxStore>().OfType<IInboxCleanupStore>(),
            store => store.ModuleName,
            "inbox");

    private static TStore[] GetUniqueStores<TStore>(
        IEnumerable<TStore> stores,
        Func<TStore, string> getModuleName,
        string journal)
    {
        TStore[] result = stores.ToArray();
        IGrouping<string, TStore>? duplicate = result
            .GroupBy(
                store => IntegrationEventNaming.NormalizeModuleName(getModuleName(store)),
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} {journal} cleanup stores are registered for module '{duplicate.Key}'.");
        }

        return result;
    }

    private void TryRecordDeleted(string moduleName, string journal, int count)
    {
        try
        {
            metrics.RecordDeleted(moduleName, journal, count);
        }
        catch (Exception)
        {
            // Metrics are observability only; exporter failures must not stop cleanup.
        }
    }
}

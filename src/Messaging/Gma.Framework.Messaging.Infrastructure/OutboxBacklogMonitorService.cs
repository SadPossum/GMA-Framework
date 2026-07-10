namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class OutboxBacklogMonitorService(
    IServiceScopeFactory scopeFactory,
    ISystemClock clock,
    IOptions<OutboxOptions> options,
    OutboxMetrics metrics,
    ILogger<OutboxBacklogMonitorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(options.Value.EffectiveBacklogPollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await this.ObserveAsync(stoppingToken).ConfigureAwait(false);

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

    private async Task ObserveAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IOutboxBacklogReader[] readers = [.. scope.ServiceProvider
            .GetServices<IOutboxStore>()
            .OfType<IOutboxBacklogReader>()];

        foreach (IOutboxBacklogReader reader in readers)
        {
            try
            {
                OutboxBacklogSnapshot snapshot = await reader
                    .GetBacklogAsync(clock.UtcNow, cancellationToken)
                    .ConfigureAwait(false);
                metrics.RecordBacklog(snapshot);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Failed to observe outbox backlog for module {ModuleName}",
                    reader.ModuleName);
            }
        }
    }
}

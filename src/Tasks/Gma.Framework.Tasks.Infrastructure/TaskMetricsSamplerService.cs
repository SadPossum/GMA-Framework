namespace Gma.Framework.Tasks.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Tasks;
using Gma.Framework.Observability.Infrastructure;

internal sealed class TaskMetricsSamplerService(
    IServiceScopeFactory scopeFactory,
    IOptions<TaskWorkerOptions> options,
    TaskMetrics metrics,
    ILogger<TaskMetricsSamplerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TaskWorkerOptions currentOptions = options.Value;
        if (!currentOptions.Enabled || !currentOptions.MetricsSamplerEnabled)
        {
            logger.LogInformation("Task metrics sampler is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.SampleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Task metrics sampling failed.");
            }

            await Task.Delay(currentOptions.EffectiveMetricsSamplerPollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SampleAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();

        TaskRunStats stats = await store.GetStatsAsync(new TaskRunStatsFilter(), cancellationToken)
            .ConfigureAwait(false);
        metrics.RecordSnapshot(stats);
    }
}

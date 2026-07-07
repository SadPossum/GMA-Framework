namespace Gma.Framework.Tasks.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Observability.Infrastructure;

internal sealed class TaskTimeoutScannerService(
    IServiceScopeFactory scopeFactory,
    IOptions<TaskWorkerOptions> options,
    TaskMetrics metrics,
    ILogger<TaskTimeoutScannerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TaskWorkerOptions currentOptions = options.Value;
        if (!currentOptions.Enabled || !currentOptions.TimeoutScannerEnabled)
        {
            logger.LogInformation("Task timeout scanner is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.ScanAsync(currentOptions, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Task timeout scanner iteration failed; the scanner will retry.");
            }

            await Task.Delay(currentOptions.EffectiveTimeoutScannerPollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ScanAsync(TaskWorkerOptions currentOptions, CancellationToken stoppingToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        ISystemClock clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();

        IReadOnlyList<TaskRunSummary> timedOut = await store.MarkStaleTimedOutAsync(
                clock.UtcNow,
                currentOptions.EffectiveStaleHeartbeatTimeout,
                currentOptions.EffectiveTimeoutScannerBatchSize,
                stoppingToken)
            .ConfigureAwait(false);

        foreach (TaskRunSummary taskRun in timedOut)
        {
            TryRecordTimedOut(metrics, taskRun);
        }

        if (timedOut.Count > 0)
        {
            logger.LogWarning("Marked {Count} stale task run(s) as timed out.", timedOut.Count);
        }
    }

    private static void TryRecordTimedOut(TaskMetrics metrics, TaskRunSummary taskRun)
    {
        try
        {
            metrics.RecordTimedOut(taskRun.ModuleName, taskRun.TaskName, taskRun.WorkerGroup);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }
}

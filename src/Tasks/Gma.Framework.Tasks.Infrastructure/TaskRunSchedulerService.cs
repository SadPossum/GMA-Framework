namespace Gma.Framework.Tasks.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;

internal sealed class TaskRunSchedulerService(
    IServiceScopeFactory scopeFactory,
    IOptions<TaskRunSchedulerOptions> options,
    ILogger<TaskRunSchedulerService> logger)
    : BackgroundService
{
    private readonly Dictionary<string, DateTimeOffset> lastOccurrenceBySchedule = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TaskRunSchedulerOptions currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            logger.LogInformation("Task run scheduler is disabled.");
            return;
        }

        logger.LogInformation("Task run scheduler started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.TickAsync(currentOptions, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Task run scheduler tick failed with {ExceptionType}; the scheduler will retry.",
                    exception.GetType().Name);
            }

            await Task.Delay(currentOptions.EffectivePollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task TickAsync(
        TaskRunSchedulerOptions currentOptions,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IEnumerable<ITaskScheduleProvider> providers = scope.ServiceProvider.GetServices<ITaskScheduleProvider>();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        IIdGenerator idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        ISystemClock clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();
        DateTimeOffset nowUtc = clock.UtcNow;

        foreach (ITaskScheduleProvider provider in providers)
        {
            IReadOnlyList<ScheduledTaskDefinition> schedules = await provider.GetSchedulesAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (ScheduledTaskDefinition schedule in schedules)
            {
                await this.TryEnqueueDueScheduleAsync(
                        store,
                        idGenerator,
                        currentOptions,
                        schedule,
                        nowUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task TryEnqueueDueScheduleAsync(
        ITaskRunStore store,
        IIdGenerator idGenerator,
        TaskRunSchedulerOptions options,
        ScheduledTaskDefinition schedule,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        DateTimeOffset occurrenceUtc = GetOccurrenceStartUtc(nowUtc, schedule.Interval);
        string scheduleKey = $"{schedule.ModuleName}:{schedule.ScheduleName}:{schedule.TaskName}:v{schedule.PayloadVersion}:{schedule.ScopeId ?? "global"}";

        if (!this.lastOccurrenceBySchedule.TryGetValue(scheduleKey, out DateTimeOffset lastOccurrenceUtc))
        {
            this.lastOccurrenceBySchedule[scheduleKey] = schedule.RunOnStart
                ? DateTimeOffset.MinValue
                : occurrenceUtc;

            if (!schedule.RunOnStart)
            {
                return;
            }

            lastOccurrenceUtc = DateTimeOffset.MinValue;
        }

        if (occurrenceUtc <= lastOccurrenceUtc)
        {
            return;
        }

        TaskRunRequest request = new(
            idGenerator.NewId(),
            schedule.ModuleName,
            schedule.TaskName,
            schedule.PayloadJson,
            nowUtc,
            nowUtc,
            schedule.WorkerGroup,
            schedule.ScopeId,
            correlationId: null,
            requestedBy: options.RequestedBy,
            schedule.MaxAttempts,
            schedule.PayloadVersion,
            schedule.CreateDeduplicationKey(occurrenceUtc));

        await store.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
        this.lastOccurrenceBySchedule[scheduleKey] = occurrenceUtc;

        logger.LogInformation(
            "Enqueued scheduled task {ModuleName}.{TaskName} for occurrence {OccurrenceUtc}.",
            schedule.ModuleName,
            schedule.TaskName,
            occurrenceUtc);
    }

    private static DateTimeOffset GetOccurrenceStartUtc(DateTimeOffset nowUtc, TimeSpan interval)
    {
        long ticks = nowUtc.UtcTicks - (nowUtc.UtcTicks % interval.Ticks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}

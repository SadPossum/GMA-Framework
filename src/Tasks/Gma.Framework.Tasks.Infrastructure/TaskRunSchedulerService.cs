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
    private readonly Dictionary<string, ScheduleCursor> scheduleCursors = new(StringComparer.Ordinal);
    private long tickGeneration;

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
        long generation = unchecked(++this.tickGeneration);

        foreach (ITaskScheduleProvider provider in providers)
        {
            await foreach (ScheduledTaskDefinition schedule in provider
                .GetSchedulesAsync(cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                await this.TryEnqueueDueScheduleAsync(
                        store,
                        idGenerator,
                        currentOptions,
                        schedule,
                        nowUtc,
                        generation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        this.PruneMissingSchedules(generation);
    }

    private async Task TryEnqueueDueScheduleAsync(
        ITaskRunStore store,
        IIdGenerator idGenerator,
        TaskRunSchedulerOptions options,
        ScheduledTaskDefinition schedule,
        DateTimeOffset nowUtc,
        long generation,
        CancellationToken cancellationToken)
    {
        DateTimeOffset occurrenceUtc = GetOccurrenceStartUtc(nowUtc, schedule.Interval);
        string scheduleKey = $"{schedule.ModuleName}:{schedule.ScheduleName}:{schedule.TaskName}:v{schedule.PayloadVersion}:{schedule.ScopeId ?? "global"}";

        if (!this.scheduleCursors.TryGetValue(scheduleKey, out ScheduleCursor cursor))
        {
            cursor = new(
                schedule.RunOnStart ? DateTimeOffset.MinValue : occurrenceUtc,
                generation);
            this.scheduleCursors[scheduleKey] = cursor;

            if (!schedule.RunOnStart)
            {
                return;
            }
        }
        else
        {
            cursor = cursor with { SeenGeneration = generation };
            this.scheduleCursors[scheduleKey] = cursor;
        }

        if (occurrenceUtc <= cursor.LastOccurrenceUtc)
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
        this.scheduleCursors[scheduleKey] = new(occurrenceUtc, generation);

        logger.LogInformation(
            "Enqueued scheduled task {ModuleName}.{TaskName} for occurrence {OccurrenceUtc}.",
            schedule.ModuleName,
            schedule.TaskName,
            occurrenceUtc);
    }

    private void PruneMissingSchedules(long generation)
    {
        List<string>? missingScheduleKeys = null;
        foreach ((string scheduleKey, ScheduleCursor cursor) in this.scheduleCursors)
        {
            if (cursor.SeenGeneration == generation)
            {
                continue;
            }

            missingScheduleKeys ??= [];
            missingScheduleKeys.Add(scheduleKey);
        }

        if (missingScheduleKeys is null)
        {
            return;
        }

        foreach (string scheduleKey in missingScheduleKeys)
        {
            this.scheduleCursors.Remove(scheduleKey);
        }
    }

    private static DateTimeOffset GetOccurrenceStartUtc(DateTimeOffset nowUtc, TimeSpan interval)
    {
        long ticks = nowUtc.UtcTicks - (nowUtc.UtcTicks % interval.Ticks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private readonly record struct ScheduleCursor(
        DateTimeOffset LastOccurrenceUtc,
        long SeenGeneration);
}

namespace Gma.Framework.Tasks.Infrastructure;

using System.Diagnostics;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Resilience;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Runtime.Workers;
using Gma.Framework.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class TaskWorkerService(
    IServiceScopeFactory scopeFactory,
    IOptions<TaskWorkerOptions> options,
    IIdGenerator idGenerator,
    TaskMetrics metrics,
    ILogger<TaskWorkerService> logger)
    : BackgroundService
{
    private readonly string workerId = CreateWorkerId(options.Value, idGenerator);
    private readonly string nodeId = CreateNodeId(options.Value);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TaskWorkerOptions currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            logger.LogInformation("Task worker runtime is disabled.");
            return;
        }

        logger.LogInformation(
            "Task worker runtime started with worker id {WorkerId} on node {NodeId}.",
            this.workerId,
            this.nodeId);

        IReadOnlyList<string> workerGroups = currentOptions.EffectiveWorkerGroups;
        List<Task> running = [];
        int nextWorkerGroupIndex = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                running.RemoveAll(task => task.IsCompleted);
                int availableCapacity = currentOptions.EffectiveMaxConcurrency - running.Count;

                for (int checkedGroups = 0;
                     checkedGroups < workerGroups.Count && availableCapacity > 0;
                     checkedGroups++)
                {
                    string workerGroup = workerGroups[nextWorkerGroupIndex];
                    nextWorkerGroupIndex = (nextWorkerGroupIndex + 1) % workerGroups.Count;
                    int claimSize = Math.Min(currentOptions.EffectiveBatchSize, availableCapacity);
                    IReadOnlyList<TaskRunLease> leases = await this.TryClaimAsync(
                            workerGroup,
                            claimSize,
                            currentOptions,
                            stoppingToken)
                        .ConfigureAwait(false);

                    foreach (TaskRunLease lease in leases)
                    {
                        TryRecordClaimed(metrics, lease);
                        running.Add(this.ProcessLeaseSafelyAsync(lease, currentOptions, stoppingToken));
                    }

                    availableCapacity -= leases.Count;
                }

                if (running.Count == 0)
                {
                    await Task.Delay(currentOptions.EffectivePollInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (running.Count >= currentOptions.EffectiveMaxConcurrency)
                {
                    await Task.WhenAny(running).ConfigureAwait(false);
                    continue;
                }

                Task pollDelay = Task.Delay(currentOptions.EffectivePollInterval, stoppingToken);
                _ = await Task.WhenAny(running.Append(pollDelay)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await Task.WhenAll(running).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task<IReadOnlyList<TaskRunLease>> TryClaimAsync(
        string workerGroup,
        int batchSize,
        TaskWorkerOptions currentOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
            ISystemClock clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();

            TaskWorkerClaim claim = new(
                workerGroup,
                this.workerId,
                this.nodeId,
                clock.UtcNow,
                batchSize,
                currentOptions.EffectiveLeaseDuration);

            IReadOnlyList<TaskRunLease> leases = await store.ClaimReadyAsync(claim, cancellationToken)
                .ConfigureAwait(false);
            if (leases.Count > batchSize)
            {
                logger.LogWarning(
                    "Task run store returned {LeaseCount} leases for a claim limited to {BatchSize}; all claimed leases will run to preserve ownership.",
                    leases.Count,
                    batchSize);
            }

            return leases;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Task worker failed to claim work for group {WorkerGroup}; other groups will continue.",
                workerGroup);
            return [];
        }
    }

    private async Task ProcessLeaseSafelyAsync(
        TaskRunLease lease,
        TaskWorkerOptions currentOptions,
        CancellationToken stoppingToken)
    {
        try
        {
            await this.ProcessLeaseAsync(lease, currentOptions, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Task run {RunId} for {Module}.{Task} failed outside handler execution; the lease will expire for retry.",
                lease.RunId,
                lease.ModuleName,
                lease.TaskName);
        }
    }

    private async Task ProcessLeaseAsync(
        TaskRunLease lease,
        TaskWorkerOptions currentOptions,
        CancellationToken stoppingToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        ITaskHandlerRegistry registry = scope.ServiceProvider.GetRequiredService<ITaskHandlerRegistry>();
        IReadOnlyList<ITaskExecutionContextContributor> contextContributors = scope.ServiceProvider
            .GetServices<ITaskExecutionContextContributor>()
            .ToArray();
        ISystemClock clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();
        TaskExecutionContext context = lease.CreateExecutionContext();
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (lease.CancellationRequested)
        {
            TaskRunMutationOutcome outcome = await store
                .MarkCanceledAsync(context, clock.UtcNow, stoppingToken)
                .ConfigureAwait(false);
            this.RecordTerminalMutation(outcome, lease, "canceled", stopwatch.Elapsed);
            return;
        }

        TaskHandlerRegistration? registration = registry.Find(lease.ModuleName, lease.TaskName, lease.PayloadVersion);
        if (registration is null)
        {
            TaskRunMutationOutcome outcome = await store.MarkFailedAsync(
                    context,
                    $"No task handler is registered for {lease.ModuleName}.{lease.TaskName}.",
                    clock.UtcNow,
                    retryAtUtc: null,
                    stoppingToken)
                .ConfigureAwait(false);
            this.RecordTerminalMutation(outcome, lease, "failed", stopwatch.Elapsed);
            return;
        }

        if (!string.Equals(registration.WorkerGroup, lease.WorkerGroup, StringComparison.Ordinal))
        {
            TaskRunMutationOutcome outcome = await store.MarkFailedAsync(
                    context,
                    $"Task handler {registration.HandlerType.FullName} is registered for worker group {registration.WorkerGroup}, but the run was leased for {lease.WorkerGroup}.",
                    clock.UtcNow,
                    retryAtUtc: null,
                    stoppingToken)
                .ConfigureAwait(false);
            this.RecordTerminalMutation(outcome, lease, "failed", stopwatch.Elapsed);
            return;
        }

        TaskExecutionContextPreparationContext preparationContext = new(lease, registration, context);
        TaskExecutionContextPreparationResult preparationResult = await PrepareExecutionContextAsync(
                contextContributors,
                preparationContext,
                stoppingToken)
            .ConfigureAwait(false);
        if (preparationResult.IsFailure)
        {
            TaskRunMutationOutcome outcome = await store.MarkFailedAsync(
                    context,
                    preparationResult.ErrorMessage!,
                    clock.UtcNow,
                    retryAtUtc: null,
                    stoppingToken)
                .ConfigureAwait(false);
            this.RecordTerminalMutation(outcome, lease, "failed", stopwatch.Elapsed);
            return;
        }

        TaskRunMutationOutcome startOutcome = await store.MarkStartedAsync(context, clock.UtcNow, stoppingToken)
            .ConfigureAwait(false);
        if (startOutcome != TaskRunMutationOutcome.Applied)
        {
            logger.LogInformation(
                "Task run {RunId} for {Module}.{Task} lost its lease before handler execution ({Outcome}).",
                lease.RunId,
                lease.ModuleName,
                lease.TaskName,
                startOutcome);
            return;
        }

        try
        {
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(currentOptions.EffectiveHandlerTimeout);
            using CancellationTokenSource heartbeatStop = new();
            Task heartbeat = this.RunAutomaticHeartbeatAsync(
                context,
                currentOptions.EffectiveHeartbeatInterval,
                timeout,
                heartbeatStop.Token);

            try
            {
                await TaskHandlerInvoker.InvokeAsync(
                        scope.ServiceProvider,
                        registration,
                        lease.PayloadJson,
                        context,
                        timeout.Token)
                    .ConfigureAwait(false);
            }
            finally
            {
                await StopHeartbeatAsync(heartbeatStop, heartbeat).ConfigureAwait(false);
            }

            TaskRunMutationOutcome outcome = await store
                .MarkSucceededAsync(context, clock.UtcNow, CancellationToken.None)
                .ConfigureAwait(false);
            this.RecordTerminalMutation(outcome, lease, "success", stopwatch.Elapsed);
        }
        catch (TaskRunCanceledException exception)
        {
            TaskRunMutationOutcome outcome = await store
                .MarkCanceledAsync(context, clock.UtcNow, CancellationToken.None)
                .ConfigureAwait(false);
            logger.LogInformation(
                exception,
                "Task run {RunId} for {Module}.{Task} cooperatively canceled.",
                lease.RunId,
                lease.ModuleName,
                lease.TaskName);
            this.RecordTerminalMutation(outcome, lease, "canceled", stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Task run {RunId} for {Module}.{Task} stopped before completion; the lease will expire for a later retry.",
                lease.RunId,
                lease.ModuleName,
                lease.TaskName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            DateTimeOffset failedAtUtc = clock.UtcNow;
            DateTimeOffset retryAtUtc = failedAtUtc.Add(GetRetryDelay(lease.Attempt, currentOptions));
            TaskRunMutationOutcome outcome = await store.MarkFailedAsync(
                    context,
                    GetErrorMessage(exception),
                    failedAtUtc,
                    retryAtUtc,
                    CancellationToken.None)
                .ConfigureAwait(false);
            this.RecordTerminalMutation(outcome, lease, "failure", stopwatch.Elapsed);
        }
        finally
        {
            await CleanupExecutionContextAsync(contextContributors, preparationContext).ConfigureAwait(false);
        }
    }

    private async Task RunAutomaticHeartbeatAsync(
        TaskExecutionContext context,
        TimeSpan heartbeatInterval,
        CancellationTokenSource executionCancellation,
        CancellationToken heartbeatStop)
    {
        using PeriodicTimer timer = new(heartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(heartbeatStop).ConfigureAwait(false))
            {
                using IServiceScope heartbeatScope = scopeFactory.CreateScope();
                ITaskRunStore reporter = heartbeatScope.ServiceProvider.GetRequiredService<ITaskRunStore>();
                ISystemClock heartbeatClock = heartbeatScope.ServiceProvider.GetRequiredService<ISystemClock>();
                TaskRunMutationOutcome outcome = await reporter
                    .ReportHeartbeatAsync(context, heartbeatClock.UtcNow, heartbeatStop)
                    .ConfigureAwait(false);
                if (outcome != TaskRunMutationOutcome.Applied)
                {
                    executionCancellation.Cancel();
                    throw new InvalidOperationException(
                        $"Task lease heartbeat was rejected with outcome '{outcome}'.");
                }
            }
        }
        catch (OperationCanceledException) when (heartbeatStop.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            executionCancellation.Cancel();
            throw new InvalidOperationException("Automatic task heartbeat failed.", exception);
        }
    }

    private static async Task StopHeartbeatAsync(CancellationTokenSource heartbeatStop, Task heartbeat)
    {
        await heartbeatStop.CancelAsync().ConfigureAwait(false);
        await heartbeat.ConfigureAwait(false);
    }

    private void RecordTerminalMutation(
        TaskRunMutationOutcome outcome,
        TaskRunLease lease,
        string status,
        TimeSpan duration)
    {
        if (outcome == TaskRunMutationOutcome.Applied)
        {
            TryRecordCompleted(metrics, lease, status, duration);
            return;
        }

        logger.LogInformation(
            "Task run {RunId} for {Module}.{Task} did not persist terminal status {Status} because the store returned {Outcome}.",
            lease.RunId,
            lease.ModuleName,
            lease.TaskName,
            status,
            outcome);
    }

    private static async ValueTask<TaskExecutionContextPreparationResult> PrepareExecutionContextAsync(
        IReadOnlyList<ITaskExecutionContextContributor> contributors,
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken)
    {
        List<ITaskExecutionContextContributor> preparedContributors = [];
        try
        {
            foreach (ITaskExecutionContextContributor contributor in contributors)
            {
                TaskExecutionContextPreparationResult result = await contributor
                    .PrepareAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                if (result.IsFailure)
                {
                    await CleanupExecutionContextAsync(preparedContributors, context).ConfigureAwait(false);
                    return result;
                }

                preparedContributors.Add(contributor);
            }
        }
        catch
        {
            await CleanupExecutionContextAsync(preparedContributors, context).ConfigureAwait(false);
            throw;
        }

        return TaskExecutionContextPreparationResult.Success();
    }

    private static async ValueTask CleanupExecutionContextAsync(
        IReadOnlyList<ITaskExecutionContextContributor> contributors,
        TaskExecutionContextPreparationContext context)
    {
        foreach (ITaskExecutionContextContributor contributor in contributors.Reverse())
        {
            try
            {
                await contributor.CleanupAsync(context, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Context cleanup is best effort; worker leases still rely on persisted task state.
            }
        }
    }

    private static string CreateWorkerId(TaskWorkerOptions options, IIdGenerator idGenerator) =>
        string.IsNullOrWhiteSpace(options.WorkerId)
            ? WorkerIds.Create(Environment.MachineName, idGenerator.NewId())
            : TaskNames.NormalizeWorkerId(options.WorkerId, nameof(options.WorkerId));

    private static string CreateNodeId(TaskWorkerOptions options) =>
        string.IsNullOrWhiteSpace(options.NodeId)
            ? TaskNames.NormalizeWorkerId(Environment.MachineName)
            : TaskNames.NormalizeWorkerId(options.NodeId, nameof(options.NodeId));

    private static TimeSpan GetRetryDelay(int attempt, TaskWorkerOptions options)
        => BoundedExponentialBackoff.Calculate(
            attempt,
            options.EffectiveRetryBaseDelay,
            options.EffectiveRetryMaxDelay,
            maximumExponent: 8);

    private static string GetErrorMessage(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return "Task handler timed out.";
        }

        return string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
    }

    private static void TryRecordClaimed(TaskMetrics metrics, TaskRunLease lease)
    {
        try
        {
            metrics.RecordClaimed(lease.ModuleName, lease.TaskName, lease.WorkerGroup);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }

    private static void TryRecordCompleted(
        TaskMetrics metrics,
        TaskRunLease lease,
        string result,
        TimeSpan elapsed)
    {
        try
        {
            metrics.RecordCompleted(lease.ModuleName, lease.TaskName, lease.WorkerGroup, result, elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }
}

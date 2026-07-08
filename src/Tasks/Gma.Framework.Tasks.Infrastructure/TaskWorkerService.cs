namespace Gma.Framework.Tasks.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime.Workers;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool processedAny = false;

                foreach (string workerGroup in currentOptions.EffectiveWorkerGroups)
                {
                    IReadOnlyList<TaskRunLease> leases = await this.ClaimAsync(
                            workerGroup,
                            currentOptions,
                            stoppingToken)
                        .ConfigureAwait(false);

                    foreach (TaskRunLease lease in leases)
                    {
                        processedAny = true;
                        TryRecordClaimed(metrics, lease);
                    }

                    await this.ProcessLeasesAsync(leases, currentOptions, stoppingToken).ConfigureAwait(false);
                }

                if (!processedAny)
                {
                    await Task.Delay(currentOptions.EffectivePollInterval, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Task worker runtime iteration failed; the worker will retry.");
                await Task.Delay(currentOptions.EffectivePollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<IReadOnlyList<TaskRunLease>> ClaimAsync(
        string workerGroup,
        TaskWorkerOptions currentOptions,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        ISystemClock clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();

        TaskWorkerClaim claim = new(
            workerGroup,
            this.workerId,
            this.nodeId,
            clock.UtcNow,
            currentOptions.EffectiveBatchSize,
            currentOptions.EffectiveLeaseDuration);

        return await store.ClaimReadyAsync(claim, cancellationToken).ConfigureAwait(false);
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
            await store.MarkCanceledAsync(context, clock.UtcNow, stoppingToken).ConfigureAwait(false);
            TryRecordCompleted(metrics, lease, "canceled", stopwatch.Elapsed);
            return;
        }

        TaskHandlerRegistration? registration = registry.Find(lease.ModuleName, lease.TaskName, lease.PayloadVersion);
        if (registration is null)
        {
            await store.MarkFailedAsync(
                    context,
                    $"No task handler is registered for {lease.ModuleName}.{lease.TaskName}.",
                    clock.UtcNow,
                    retryAtUtc: null,
                    stoppingToken)
                .ConfigureAwait(false);
            TryRecordCompleted(metrics, lease, "failed", stopwatch.Elapsed);
            return;
        }

        if (!string.Equals(registration.WorkerGroup, lease.WorkerGroup, StringComparison.Ordinal))
        {
            await store.MarkFailedAsync(
                    context,
                    $"Task handler {registration.HandlerType.FullName} is registered for worker group {registration.WorkerGroup}, but the run was leased for {lease.WorkerGroup}.",
                    clock.UtcNow,
                    retryAtUtc: null,
                    stoppingToken)
                .ConfigureAwait(false);
            TryRecordCompleted(metrics, lease, "failed", stopwatch.Elapsed);
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
            await store.MarkFailedAsync(
                    context,
                    preparationResult.ErrorMessage!,
                    clock.UtcNow,
                    retryAtUtc: null,
                    stoppingToken)
                .ConfigureAwait(false);
            TryRecordCompleted(metrics, lease, "failed", stopwatch.Elapsed);
            return;
        }

        await store.MarkStartedAsync(context, clock.UtcNow, stoppingToken).ConfigureAwait(false);

        try
        {
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(currentOptions.EffectiveHandlerTimeout);

            await TaskHandlerInvoker.InvokeAsync(
                    scope.ServiceProvider,
                    registration,
                    lease.PayloadJson,
                    context,
                    timeout.Token)
                .ConfigureAwait(false);

            await store.MarkSucceededAsync(context, clock.UtcNow, stoppingToken).ConfigureAwait(false);
            TryRecordCompleted(metrics, lease, "success", stopwatch.Elapsed);
        }
        catch (TaskRunCanceledException exception)
        {
            await store.MarkCanceledAsync(context, clock.UtcNow, CancellationToken.None).ConfigureAwait(false);
            logger.LogInformation(
                exception,
                "Task run {RunId} for {Module}.{Task} cooperatively canceled.",
                lease.RunId,
                lease.ModuleName,
                lease.TaskName);
            TryRecordCompleted(metrics, lease, "canceled", stopwatch.Elapsed);
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
            await store.MarkFailedAsync(
                    context,
                    GetErrorMessage(exception),
                    failedAtUtc,
                    retryAtUtc,
                    CancellationToken.None)
                .ConfigureAwait(false);
            TryRecordCompleted(metrics, lease, "failure", stopwatch.Elapsed);
        }
        finally
        {
            await CleanupExecutionContextAsync(contextContributors, preparationContext).ConfigureAwait(false);
        }
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

    private async Task ProcessLeasesAsync(
        IReadOnlyList<TaskRunLease> leases,
        TaskWorkerOptions currentOptions,
        CancellationToken stoppingToken)
    {
        if (leases.Count == 0)
        {
            return;
        }

        using SemaphoreSlim semaphore = new(currentOptions.EffectiveMaxConcurrency);
        Task[] tasks = leases
            .Select(async lease =>
            {
                await semaphore.WaitAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    await this.ProcessLeaseAsync(lease, currentOptions, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
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
                finally
                {
                    semaphore.Release();
                }
            })
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
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
    {
        double multiplier = Math.Pow(2, Math.Max(0, Math.Min(attempt - 1, 8)));
        TimeSpan delay = TimeSpan.FromMilliseconds(options.EffectiveRetryBaseDelay.TotalMilliseconds * multiplier);
        return delay <= options.EffectiveRetryMaxDelay
            ? delay
            : options.EffectiveRetryMaxDelay;
    }

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

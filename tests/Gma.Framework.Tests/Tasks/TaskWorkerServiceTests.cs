namespace Gma.Framework.Tests.Tasks;

using System.Collections.Concurrent;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class TaskWorkerServiceTests
{
    [Fact]
    public async Task Worker_claims_only_available_execution_capacity()
    {
        WorkerTestStore store = new();
        WorkerGate gate = new();
        store.Enqueue("alpha", count: 4);

        using IHost host = CreateHost(store, gate, ["alpha"], maxConcurrency: 2, batchSize: 10);
        await host.StartAsync();
        Assert.True(await store.WaitForStartedAsync(2, TimeSpan.FromSeconds(2)));

        Assert.All(store.Claims, claim => Assert.InRange(claim.MaxRuns, 1, 2));
        Assert.Equal(2, store.ClaimedCount);
        Assert.Equal(2, store.MaximumRunningCount);

        gate.Release();
        Assert.True(await store.WaitForSucceededAsync(4, TimeSpan.FromSeconds(2)));
        await host.StopAsync();
    }

    [Fact]
    public async Task Worker_rotates_across_groups_before_waiting_for_running_work()
    {
        WorkerTestStore store = new();
        WorkerGate gate = new();
        store.Enqueue("alpha", count: 1);
        store.Enqueue("beta", count: 1);

        using IHost host = CreateHost(store, gate, ["alpha", "beta"], maxConcurrency: 2, batchSize: 10);
        await host.StartAsync();
        Assert.True(await store.WaitForStartedAsync(2, TimeSpan.FromSeconds(2)));

        Assert.Contains("alpha", store.StartedGroups);
        Assert.Contains("beta", store.StartedGroups);

        gate.Release();
        Assert.True(await store.WaitForSucceededAsync(2, TimeSpan.FromSeconds(2)));
        await host.StopAsync();
    }

    [Fact]
    public async Task Worker_automatically_heartbeats_a_running_handler()
    {
        WorkerTestStore store = new();
        WorkerGate gate = new();
        store.Enqueue("alpha", count: 1);

        using IHost host = CreateHost(
            store,
            gate,
            ["alpha"],
            maxConcurrency: 1,
            batchSize: 10,
            leaseDuration: TimeSpan.FromMilliseconds(300),
            heartbeatInterval: TimeSpan.FromMilliseconds(50));
        await host.StartAsync();
        Assert.True(await store.WaitForStartedAsync(1, TimeSpan.FromSeconds(2)));
        Assert.True(await store.WaitForHeartbeatsAsync(2, TimeSpan.FromSeconds(2)));

        gate.Release();
        Assert.True(await store.WaitForSucceededAsync(1, TimeSpan.FromSeconds(2)));
        int completedHeartbeatCount = store.HeartbeatCount;
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        Assert.Equal(completedHeartbeatCount, store.HeartbeatCount);
        await host.StopAsync();
    }

    [Fact]
    public async Task Worker_cancels_and_fails_a_handler_when_automatic_heartbeat_fails()
    {
        WorkerTestStore store = new()
        {
            HeartbeatFailuresBeforeSuccess = 1,
        };
        WorkerGate gate = new();
        store.Enqueue("alpha", count: 1);

        using IHost host = CreateHost(
            store,
            gate,
            ["alpha"],
            maxConcurrency: 1,
            batchSize: 1,
            leaseDuration: TimeSpan.FromMilliseconds(300),
            heartbeatInterval: TimeSpan.FromMilliseconds(50));
        await host.StartAsync();

        Assert.True(await store.WaitForFailedAsync(1, TimeSpan.FromSeconds(2)));
        Assert.Contains("Automatic task heartbeat failed", store.LastFailure, StringComparison.Ordinal);
        await host.StopAsync();
    }

    private static IHost CreateHost(
        WorkerTestStore store,
        WorkerGate gate,
        IReadOnlyList<string> workerGroups,
        int maxConcurrency,
        int batchSize,
        TimeSpan? leaseDuration = null,
        TimeSpan? heartbeatInterval = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        Dictionary<string, string?> configuration = new()
        {
            ["Tasks:Worker:Enabled"] = "true",
            ["Tasks:Worker:BatchSize"] = batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Tasks:Worker:MaxConcurrency"] = maxConcurrency.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Tasks:Worker:PollInterval"] = "00:00:00.010",
            ["Tasks:Worker:LeaseDuration"] = (leaseDuration ?? TimeSpan.FromSeconds(1)).ToString("c"),
            ["Tasks:Worker:HeartbeatInterval"] = (heartbeatInterval ?? TimeSpan.FromMilliseconds(100)).ToString("c"),
            ["Tasks:Worker:HandlerTimeout"] = "00:00:05",
            ["Tasks:Worker:TimeoutScannerEnabled"] = "false",
            ["Tasks:Worker:MetricsSamplerEnabled"] = "false",
            ["Tasks:Worker:WorkerId"] = "worker-test",
            ["Tasks:Worker:NodeId"] = "node-test",
        };
        for (int index = 0; index < workerGroups.Count; index++)
        {
            configuration[$"Tasks:Worker:WorkerGroups:{index}"] = workerGroups[index];
        }

        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITaskRunStore>(store);
        builder.Services.AddSingleton(gate);
        foreach (string workerGroup in workerGroups)
        {
            builder.Services.AddTaskHandler<WorkerTestPayload, BlockingWorkerHandler>(
                "task-tests",
                $"run-{workerGroup}",
                workerGroup);
        }

        builder.AddTaskWorkerRuntime();
        return builder.Build();
    }

    private sealed record WorkerTestPayload(string WorkerGroup) : ITaskPayload;

    private sealed class BlockingWorkerHandler(WorkerGate gate) : ITaskHandler<WorkerTestPayload>
    {
        public Task HandleAsync(
            WorkerTestPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) => gate.WaitAsync(cancellationToken);
    }

    private sealed class WorkerGate
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitAsync(CancellationToken cancellationToken) =>
            this.completion.Task.WaitAsync(cancellationToken);

        public void Release() => this.completion.TrySetResult();
    }

    private sealed class WorkerTestStore : ITaskRunStore
    {
        private readonly Lock sync = new();
        private readonly Dictionary<string, Queue<Guid>> readyByGroup = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<TaskWorkerClaim> claims = new();
        private readonly ConcurrentQueue<string> startedGroups = new();
        private int claimedCount;
        private int failedCount;
        private int heartbeatAttempts;
        private int heartbeatCount;
        private int maximumRunningCount;
        private int runningCount;
        private int startedCount;
        private int succeededCount;

        public int HeartbeatFailuresBeforeSuccess { get; init; }
        public IReadOnlyCollection<TaskWorkerClaim> Claims => this.claims.ToArray();
        public IReadOnlyCollection<string> StartedGroups => this.startedGroups.ToArray();
        public int ClaimedCount => Volatile.Read(ref this.claimedCount);
        public int HeartbeatCount => Volatile.Read(ref this.heartbeatCount);
        public string LastFailure { get; private set; } = string.Empty;
        public int MaximumRunningCount => Volatile.Read(ref this.maximumRunningCount);

        public void Enqueue(string workerGroup, int count)
        {
            lock (this.sync)
            {
                if (!this.readyByGroup.TryGetValue(workerGroup, out Queue<Guid>? ready))
                {
                    ready = new();
                    this.readyByGroup.Add(workerGroup, ready);
                }

                for (int index = 0; index < count; index++)
                {
                    ready.Enqueue(Guid.NewGuid());
                }
            }
        }

        public Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(
            TaskWorkerClaim claim,
            CancellationToken cancellationToken)
        {
            this.claims.Enqueue(claim);
            List<TaskRunLease> leases = [];
            lock (this.sync)
            {
                if (this.readyByGroup.TryGetValue(claim.WorkerGroup, out Queue<Guid>? ready))
                {
                    while (leases.Count < claim.MaxRuns && ready.TryDequeue(out Guid runId))
                    {
                        leases.Add(new(
                            runId,
                            "task-tests",
                            $"run-{claim.WorkerGroup}",
                            claim.WorkerGroup,
                            claim.WorkerId,
                            claim.NodeId,
                            System.Text.Json.JsonSerializer.Serialize(new WorkerTestPayload(claim.WorkerGroup)),
                            attempt: 1,
                            claim.ClaimedAtUtc,
                            claim.LockedUntilUtc));
                    }
                }
            }

            Interlocked.Add(ref this.claimedCount, leases.Count);
            return Task.FromResult<IReadOnlyList<TaskRunLease>>(leases);
        }

        public Task MarkStartedAsync(
            TaskExecutionContext context,
            DateTimeOffset startedAtUtc,
            CancellationToken cancellationToken)
        {
            this.startedGroups.Enqueue(context.WorkerGroup);
            Interlocked.Increment(ref this.startedCount);
            int running = Interlocked.Increment(ref this.runningCount);
            UpdateMaximum(ref this.maximumRunningCount, running);
            return Task.CompletedTask;
        }

        public Task MarkSucceededAsync(
            TaskExecutionContext context,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken)
        {
            Interlocked.Decrement(ref this.runningCount);
            Interlocked.Increment(ref this.succeededCount);
            return Task.CompletedTask;
        }

        public Task ReportHeartbeatAsync(
            TaskExecutionContext context,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref this.heartbeatAttempts);
            if (attempt <= this.HeartbeatFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Heartbeat storage unavailable.");
            }

            Interlocked.Increment(ref this.heartbeatCount);
            return Task.CompletedTask;
        }

        public Task<bool> WaitForStartedAsync(int count, TimeSpan timeout) =>
            WaitForCountAsync(() => Volatile.Read(ref this.startedCount), count, timeout);

        public Task<bool> WaitForSucceededAsync(int count, TimeSpan timeout) =>
            WaitForCountAsync(() => Volatile.Read(ref this.succeededCount), count, timeout);

        public Task<bool> WaitForHeartbeatsAsync(int count, TimeSpan timeout) =>
            WaitForCountAsync(() => Volatile.Read(ref this.heartbeatCount), count, timeout);

        public Task<bool> WaitForFailedAsync(int count, TimeSpan timeout) =>
            WaitForCountAsync(() => Volatile.Read(ref this.failedCount), count, timeout);

        public Task EnqueueAsync(TaskRunRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TaskRunSummary>> ListAsync(
            TaskRunFilter filter,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskRunDetails?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunStats> GetStatsAsync(TaskRunStatsFilter filter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RetryAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset scheduledAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkCanceledAsync(
            TaskExecutionContext context,
            DateTimeOffset canceledAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkFailedAsync(
            TaskExecutionContext context,
            string error,
            DateTimeOffset failedAtUtc,
            DateTimeOffset? retryAtUtc,
            CancellationToken cancellationToken)
        {
            this.LastFailure = error;
            Interlocked.Decrement(ref this.runningCount);
            Interlocked.Increment(ref this.failedCount);
            return Task.CompletedTask;
        }

        public Task ReportProgressAsync(
            TaskExecutionContext context,
            TaskProgress progress,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RequestCancellationAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset requestedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
            DateTimeOffset nowUtc,
            TimeSpan staleAfter,
            int maxRuns,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EnqueueControlMessageAsync(TaskControlMessage message, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkHandledAsync(
            TaskExecutionContext context,
            Guid messageId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkFailedAsync(
            TaskExecutionContext context,
            Guid messageId,
            string error,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        private static async Task<bool> WaitForCountAsync(
            Func<int> read,
            int expected,
            TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (read() >= expected)
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
            }

            return read() >= expected;
        }

        private static void UpdateMaximum(ref int target, int candidate)
        {
            int current = Volatile.Read(ref target);
            while (candidate > current)
            {
                int observed = Interlocked.CompareExchange(ref target, candidate, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}

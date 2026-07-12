namespace Gma.Framework.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TaskRuntimeInfrastructureTests
{
    private static readonly Guid RunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Task_run_enforces_lease_owner_retry_and_success_transitions()
    {
        TaskRun taskRun = TaskRun.Enqueue(new TaskRunRequest(
            RunId,
            "task-samples",
            "generate-report",
            "{\"reportName\":\"daily\",\"expectedRows\":10}",
            Now,
            Now,
            workerGroup: "samples",
            scopeId: "tenant-a",
            maxAttempts: 2));

        TaskRunLease firstLease = taskRun.Claim(new TaskWorkerClaim(
            "samples",
            "worker-a",
            "node-a",
            Now,
            maxRuns: 1,
            leaseDuration: TimeSpan.FromMinutes(1)));
        TaskExecutionContext context = firstLease.CreateExecutionContext();
        TaskExecutionContext wrongWorkerContext = new(
            RunId,
            "task-samples",
            "generate-report",
            "samples",
            "worker-b",
            "node-a",
            attempt: 1,
            scopeId: "tenant-a");

        Assert.Throws<InvalidOperationException>(() => taskRun.MarkStarted(wrongWorkerContext, Now.AddSeconds(1)));

        taskRun.MarkStarted(context, Now.AddSeconds(1));
        taskRun.MarkProgress(context, new TaskProgress(40, "halfway"), Now.AddSeconds(2));
        taskRun.MarkFailed(context, "temporary", Now.AddSeconds(3), Now.AddSeconds(10));

        Assert.Equal(TaskRunStatus.RetryScheduled, taskRun.Status);
        Assert.Equal(1, taskRun.Attempts);
        Assert.Null(taskRun.LockedBy);
        Assert.Equal(Now.AddSeconds(10), taskRun.NextAttemptAtUtc);

        TaskRunLease secondLease = taskRun.Claim(new TaskWorkerClaim(
            "samples",
            "worker-b",
            "node-b",
            Now.AddSeconds(10),
            maxRuns: 1,
            leaseDuration: TimeSpan.FromMinutes(1)));
        TaskExecutionContext secondContext = secondLease.CreateExecutionContext();

        taskRun.MarkStarted(secondContext, Now.AddSeconds(11));
        taskRun.MarkSucceeded(secondContext, Now.AddSeconds(12));

        Assert.Equal(TaskRunStatus.Succeeded, taskRun.Status);
        Assert.Equal(2, taskRun.Attempts);
        Assert.Null(taskRun.LockedBy);
        Assert.Null(taskRun.NextAttemptAtUtc);
        Assert.Null(taskRun.LastError);
    }

    [Fact]
    public void Task_run_persists_requested_by_from_enqueue_contract()
    {
        TaskRun taskRun = TaskRun.Enqueue(new TaskRunRequest(
            RunId,
            "task-samples",
            "generate-report",
            "{}",
            Now,
            Now,
            requestedBy: " operator "));

        Assert.Equal("operator", taskRun.RequestedBy);
    }

    [Fact]
    public void Task_run_cancels_unleased_runs_immediately()
    {
        TaskRun taskRun = TaskRun.Enqueue(new TaskRunRequest(
            RunId,
            "task-samples",
            "generate-report",
            "{}",
            Now,
            Now,
            maxAttempts: 2));

        taskRun.RequestCancellation(" operator ", Now.AddSeconds(1));

        Assert.Equal(TaskRunStatus.Canceled, taskRun.Status);
        Assert.Equal(Now.AddSeconds(1), taskRun.CompletedAtUtc);
        Assert.Equal("operator", taskRun.CancellationRequestedBy);
        Assert.Equal(Now.AddSeconds(1), taskRun.CancellationRequestedAtUtc);
        Assert.Null(taskRun.LockedBy);
        Assert.Null(taskRun.NextAttemptAtUtc);
    }

    [Fact]
    public void Task_run_reclaims_cancel_requested_leases_without_rerunning_handler()
    {
        TaskRun taskRun = TaskRun.Enqueue(new TaskRunRequest(
            RunId,
            "task-samples",
            "generate-report",
            "{}",
            Now,
            Now,
            workerGroup: "samples",
            maxAttempts: 1));
        TaskRunLease firstLease = taskRun.Claim(new TaskWorkerClaim(
            "samples",
            "worker-a",
            "node-a",
            Now,
            maxRuns: 1,
            leaseDuration: TimeSpan.FromSeconds(5)));

        taskRun.MarkStarted(firstLease.CreateExecutionContext(), Now.AddSeconds(1));
        taskRun.RequestCancellation("operator", Now.AddSeconds(2));

        TaskRunLease cancellationLease = taskRun.Claim(new TaskWorkerClaim(
            "samples",
            "worker-b",
            "node-b",
            Now.AddSeconds(6),
            maxRuns: 1,
            leaseDuration: TimeSpan.FromSeconds(5)));
        TaskExecutionContext cancellationContext = cancellationLease.CreateExecutionContext();
        taskRun.MarkCanceled(cancellationContext, Now.AddSeconds(7));

        Assert.True(cancellationLease.CancellationRequested);
        Assert.True(cancellationContext.CancellationRequested);
        Assert.Equal(1, cancellationLease.Attempt);
        Assert.Equal(TaskRunStatus.Canceled, taskRun.Status);
        Assert.Null(taskRun.LockedBy);
        Assert.Null(taskRun.LastError);
    }

    [Fact]
    public void Task_run_retry_resets_terminal_runtime_state()
    {
        TaskRun taskRun = TaskRun.Enqueue(new TaskRunRequest(
            RunId,
            "task-samples",
            "generate-report",
            "{}",
            Now,
            Now,
            workerGroup: "samples",
            requestedBy: "operator",
            maxAttempts: 2,
            payloadVersion: 2,
            deduplicationKey: "sample:daily"));
        TaskRunLease lease = taskRun.Claim(new TaskWorkerClaim(
            "samples",
            "worker-a",
            "node-a",
            Now,
            maxRuns: 1,
            leaseDuration: TimeSpan.FromSeconds(5)));
        TaskExecutionContext context = lease.CreateExecutionContext();

        taskRun.MarkStarted(context, Now.AddSeconds(1));
        taskRun.MarkFailed(context, "temporary", Now.AddSeconds(2), retryAtUtc: null);
        taskRun.Retry("operator-2", Now.AddSeconds(10));

        Assert.Equal(TaskRunStatus.Queued, taskRun.Status);
        Assert.Equal(Now.AddSeconds(10), taskRun.ScheduledAtUtc);
        Assert.Equal(0, taskRun.Attempts);
        Assert.Equal(2, taskRun.PayloadVersion);
        Assert.Equal("sample:daily", taskRun.DeduplicationKey);
        Assert.Equal("operator-2", taskRun.RequestedBy);
        Assert.Null(taskRun.StartedAtUtc);
        Assert.Null(taskRun.CompletedAtUtc);
        Assert.Null(taskRun.LockedBy);
        Assert.Null(taskRun.NodeId);
        Assert.Null(taskRun.LastError);
    }

    [Fact]
    public void Task_run_heartbeat_renews_lease_when_context_carries_extension()
    {
        TaskRun taskRun = TaskRun.Enqueue(new TaskRunRequest(
            RunId,
            "task-samples",
            "slow-report",
            "{}",
            Now,
            Now,
            workerGroup: "samples",
            maxAttempts: 2));
        TaskRunLease lease = taskRun.Claim(new TaskWorkerClaim(
            "samples",
            "worker-a",
            "node-a",
            Now,
            maxRuns: 1,
            leaseDuration: TimeSpan.FromSeconds(5)));
        TaskExecutionContext context = lease.CreateExecutionContext();

        taskRun.MarkStarted(context, Now.AddSeconds(1));
        taskRun.MarkHeartbeat(context, Now.AddSeconds(4));

        Assert.Equal(Now.AddSeconds(9), taskRun.LockedUntilUtc);
        Assert.False(taskRun.CanClaim(Now.AddSeconds(6)));
        Assert.True(taskRun.CanClaim(Now.AddSeconds(10)));
    }

    [Fact]
    public void Task_run_timeout_clears_lease_and_marks_terminal_error()
    {
        TaskRun taskRun = TaskRun.Enqueue(new TaskRunRequest(
            RunId,
            "task-samples",
            "generate-report",
            "{}",
            Now,
            Now,
            workerGroup: "samples",
            maxAttempts: 2));
        TaskRunLease lease = taskRun.Claim(new TaskWorkerClaim(
            "samples",
            "worker-a",
            "node-a",
            Now,
            maxRuns: 1,
            leaseDuration: TimeSpan.FromSeconds(5)));

        taskRun.MarkStarted(lease.CreateExecutionContext(), Now.AddSeconds(1));
        taskRun.MarkTimedOut(Now.AddMinutes(10));

        Assert.Equal(TaskRunStatus.TimedOut, taskRun.Status);
        Assert.Equal(Now.AddMinutes(10), taskRun.CompletedAtUtc);
        Assert.Null(taskRun.LockedBy);
        Assert.Null(taskRun.NodeId);
        Assert.Contains("timed out", taskRun.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Task_control_message_state_tracks_delivery_handled_and_failure()
    {
        TaskControlMessage message = new(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            RunId,
            "tasks.pause",
            "{}",
            Now,
            "operator",
            Now.AddMinutes(5));
        TaskControlMessageState state = TaskControlMessageState.Enqueue(message);

        Assert.True(state.IsReadableAt(Now));

        state.MarkDelivered(Now.AddSeconds(1));
        state.MarkFailed("bad input", Now.AddSeconds(2));

        Assert.Equal(TaskControlMessageStatus.Failed, state.Status);
        Assert.Equal("bad input", state.LastError);

        state.MarkHandled(Now.AddSeconds(3));
        state.MarkFailed("late failure", Now.AddSeconds(4));

        Assert.Equal(TaskControlMessageStatus.Handled, state.Status);
        Assert.Null(state.LastError);
        Assert.False(state.IsReadableAt(Now.AddSeconds(3)));
    }

    [Fact]
    public async Task Task_control_loop_polls_and_marks_messages_through_channel()
    {
        TaskExecutionContext context = new(
            RunId,
            "task-samples",
            "slow-report",
            "samples",
            "worker-a",
            "node-a",
            attempt: 1,
            scopeId: "tenant-a");
        TaskControlMessage pause = new(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            RunId,
            TaskControlCommandNames.Pause,
            "{}",
            Now,
            "operator");
        TaskControlMessage drain = new(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            RunId,
            TaskControlCommandNames.Drain,
            "{}",
            Now,
            "operator");
        RecordingControlChannel channel = new([pause, drain]);
        TaskControlLoop loop = new(channel);

        TaskControlPollResult result = await loop.PollAsync(context, maxMessages: 10, CancellationToken.None);
        await loop.MarkHandledAsync(context, pause, CancellationToken.None);
        await loop.MarkFailedAsync(context, drain, "not ready", CancellationToken.None);

        Assert.True(result.HasMessages);
        Assert.True(result.PauseRequested);
        Assert.True(result.DrainRequested);
        Assert.False(result.CancelRequested);
        Assert.Equal(drain, result.CancellationMessage);
        Assert.Equal(context, channel.LastReadContext);
        Assert.Equal(10, channel.LastMaxMessages);
        Assert.Equal([pause.MessageId], channel.HandledMessageIds);
        Assert.Equal([(drain.MessageId, "not ready")], channel.FailedMessages);
    }

    [Fact]
    public async Task Task_control_loop_extensions_pause_until_resume_and_mark_standard_messages()
    {
        TaskExecutionContext context = CreateControlContext();
        TaskControlMessage pause = new(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            RunId,
            TaskControlCommandNames.Pause,
            "{}",
            Now,
            "operator");
        TaskControlMessage resume = new(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            RunId,
            TaskControlCommandNames.Resume,
            "{}",
            Now.AddSeconds(1),
            "operator");
        SequencedControlChannel channel = new([[pause], [], [resume]]);
        TaskControlLoop loop = new(channel);

        await loop.PauseIfRequestedAsync(
            context,
            TimeSpan.FromMilliseconds(1),
            maxMessages: 10,
            CancellationToken.None);

        Assert.Equal([pause.MessageId, resume.MessageId], channel.HandledMessageIds);
        Assert.Equal(3, channel.ReadCount);
    }

    [Fact]
    public async Task Task_control_loop_extensions_mark_cancel_and_throw_task_canceled_exception()
    {
        TaskExecutionContext context = CreateControlContext();
        TaskControlMessage cancel = new(
            Guid.Parse("12121212-1212-1212-1212-121212121212"),
            RunId,
            TaskControlCommandNames.Cancel,
            "{}",
            Now,
            "operator");
        SequencedControlChannel channel = new([[cancel]]);
        TaskControlLoop loop = new(channel);

        TaskRunCanceledException exception = await Assert.ThrowsAsync<TaskRunCanceledException>(() =>
            loop.ThrowIfCancellationRequestedAsync(context, maxMessages: 10, CancellationToken.None));

        Assert.Contains(TaskControlCommandNames.Cancel, exception.Message, StringComparison.Ordinal);
        Assert.Equal([cancel.MessageId], channel.HandledMessageIds);
    }

    [Fact]
    public void Task_run_stats_calculates_total_from_status_counts()
    {
        TaskRunStats stats = new(
        [
            new TaskRunStatusCount(TaskRunStatus.Queued, 2),
            new TaskRunStatusCount(TaskRunStatus.Running, 3),
            new TaskRunStatusCount(TaskRunStatus.Succeeded, 5)
        ]);

        Assert.Equal(10, stats.Total);
    }

    [Fact]
    public async Task Task_handler_invoker_deserializes_payload_and_invokes_handler()
    {
        ServiceCollection services = new();
        services.AddScoped<RecordingTaskHandler>();
        services.AddSingleton<RecordingTaskSink>();
        using ServiceProvider provider = services.BuildServiceProvider();
        TaskHandlerRegistration registration = TaskHandlerRegistration.Create<TestPayload, RecordingTaskHandler>(
            "task-samples",
            "generate-report");
        TaskExecutionContext context = new(
            RunId,
            "task-samples",
            "generate-report",
            TaskWorkerGroups.Default,
            "worker-a",
            "node-a",
            attempt: 1);

        await TaskHandlerInvoker.InvokeAsync(
            provider,
            registration,
            "{\"name\":\"daily\",\"count\":5}",
            context,
            CancellationToken.None);

        RecordingTaskSink sink = provider.GetRequiredService<RecordingTaskSink>();
        Assert.Equal(new TestPayload("daily", 5), sink.Payload);
        Assert.Equal(context, sink.Context);
    }

    [Fact]
    public void Task_infrastructure_registers_an_empty_handler_registry()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        builder.AddTaskInfrastructure();

        using IHost host = builder.Build();
        ITaskHandlerRegistry registry = host.Services.GetRequiredService<ITaskHandlerRegistry>();

        Assert.Empty(registry.Registrations);
        Assert.Null(registry.Find("missing-module", "missing-task"));
    }

    [Theory]
    [InlineData(0, 1000, 1000, 1000, 1000, 5000, "BatchSize")]
    [InlineData(1, 0, 1000, 1000, 1000, 5000, "PollInterval")]
    [InlineData(1, 1000, 0, 1000, 1000, 5000, "LeaseDuration")]
    [InlineData(1, 1000, 1000, 0, 1000, 5000, "HandlerTimeout")]
    [InlineData(1, 1000, 1000, 1000, 0, 5000, "RetryBaseDelay")]
    [InlineData(1, 1000, 1000, 1000, 5000, 1000, "RetryMaxDelay")]
    public void Task_worker_options_validator_rejects_invalid_values(
        int batchSize,
        int pollMilliseconds,
        int leaseMilliseconds,
        int handlerMilliseconds,
        int retryBaseMilliseconds,
        int retryMaxMilliseconds,
        string expectedFailure)
    {
        TaskWorkerOptions options = new()
        {
            BatchSize = batchSize,
            PollInterval = TimeSpan.FromMilliseconds(pollMilliseconds),
            LeaseDuration = TimeSpan.FromMilliseconds(leaseMilliseconds),
            HandlerTimeout = TimeSpan.FromMilliseconds(handlerMilliseconds),
            RetryBaseDelay = TimeSpan.FromMilliseconds(retryBaseMilliseconds),
            RetryMaxDelay = TimeSpan.FromMilliseconds(retryMaxMilliseconds),
        };

        ValidateOptionsResult result = new TaskWorkerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Fact]
    public void Task_worker_options_validator_rejects_invalid_concurrency_and_timeout_scanner_options()
    {
        TaskWorkerOptions options = new()
        {
            MaxConcurrency = 0,
            TimeoutScannerPollInterval = TimeSpan.Zero,
            StaleHeartbeatTimeout = TimeSpan.Zero,
            TimeoutScannerBatchSize = 0,
            MetricsSamplerPollInterval = TimeSpan.Zero,
        };

        ValidateOptionsResult result = new TaskWorkerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("MaxConcurrency", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("TimeoutScannerPollInterval", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("StaleHeartbeatTimeout", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("TimeoutScannerBatchSize", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("MetricsSamplerPollInterval", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(1500)]
    public void Task_worker_options_validator_rejects_unsafe_explicit_heartbeat_intervals(
        int heartbeatMilliseconds)
    {
        TaskWorkerOptions options = new()
        {
            LeaseDuration = TimeSpan.FromSeconds(1),
            HeartbeatInterval = TimeSpan.FromMilliseconds(heartbeatMilliseconds),
        };

        ValidateOptionsResult result = new TaskWorkerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("HeartbeatInterval", StringComparison.Ordinal));
    }

    [Fact]
    public void Task_worker_options_derive_heartbeat_interval_from_lease_duration()
    {
        TaskWorkerOptions options = new()
        {
            LeaseDuration = TimeSpan.FromSeconds(30),
        };

        Assert.Equal(TimeSpan.FromSeconds(10), options.EffectiveHeartbeatInterval);
    }

    [Fact]
    public void Task_run_scheduler_options_validator_rejects_invalid_options()
    {
        TaskRunSchedulerOptions options = new()
        {
            PollInterval = TimeSpan.Zero,
            RequestedBy = new string('x', TaskNames.ActorMaxLength + 1),
        };

        ValidateOptionsResult result = new TaskRunSchedulerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("PollInterval", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("RequestedBy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Task_run_scheduler_enqueues_due_code_defined_schedules()
    {
        RecordingTaskRunStore store = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tasks:Scheduler:Enabled"] = "true",
            ["Tasks:Scheduler:PollInterval"] = "00:00:00.010",
            ["Tasks:Scheduler:RequestedBy"] = "test-scheduler",
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITaskRunStore>(store);
        builder.Services.AddSingleton<ISystemClock>(new FixedClock(Now));
        builder.Services.AddSingleton<IIdGenerator>(new FixedIdGenerator(RunId));
        builder.Services.AddSingleton<ITaskScheduleProvider>(new SingleScheduleProvider());
        builder.AddTaskRunScheduling();

        using IHost host = builder.Build();
        await host.StartAsync();
        IReadOnlyList<TaskRunRequest> requests = await store.WaitForRequestsAsync(1, TimeSpan.FromSeconds(2));
        await host.StopAsync();

        TaskRunRequest request = Assert.Single(requests);
        Assert.Equal(RunId, request.RunId);
        Assert.Equal("task-samples", request.ModuleName);
        Assert.Equal("generate-report", request.TaskName);
        Assert.Equal("samples", request.WorkerGroup);
        Assert.Equal("test-scheduler", request.RequestedBy);
        Assert.Equal(
            "schedule:task-samples:generate-report:nightly-report:v1:20260703120000",
            request.DeduplicationKey);
    }

    [Fact]
    public async Task Task_worker_runtime_continues_after_transient_claim_failure()
    {
        RecordingTaskRunStore store = new()
        {
            ClaimFailuresBeforeSuccess = 1,
        };
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tasks:Worker:Enabled"] = "true",
            ["Tasks:Worker:WorkerGroups:0"] = "samples",
            ["Tasks:Worker:PollInterval"] = "00:00:00.010",
            ["Tasks:Worker:LeaseDuration"] = "00:00:01",
            ["Tasks:Worker:HandlerTimeout"] = "00:00:01",
            ["Tasks:Worker:TimeoutScannerEnabled"] = "false",
            ["Tasks:Worker:MetricsSamplerEnabled"] = "false",
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITaskRunStore>(store);
        builder.Services.AddSingleton<ISystemClock>(new FixedClock(Now));
        builder.AddTaskWorkerRuntime();

        using IHost host = builder.Build();
        await host.StartAsync();
        bool retried = await store.WaitForClaimAttemptsAsync(2, TimeSpan.FromSeconds(2));
        await host.StopAsync();

        Assert.True(retried);
    }

    [Fact]
    public async Task Task_timeout_scanner_continues_after_transient_scan_failure()
    {
        RecordingTaskRunStore store = new()
        {
            TimeoutScanFailuresBeforeSuccess = 1,
        };
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tasks:Worker:Enabled"] = "true",
            ["Tasks:Worker:WorkerGroups:0"] = "samples",
            ["Tasks:Worker:PollInterval"] = "00:01:00",
            ["Tasks:Worker:LeaseDuration"] = "00:00:01",
            ["Tasks:Worker:HandlerTimeout"] = "00:00:01",
            ["Tasks:Worker:TimeoutScannerEnabled"] = "true",
            ["Tasks:Worker:TimeoutScannerPollInterval"] = "00:00:00.010",
            ["Tasks:Worker:StaleHeartbeatTimeout"] = "00:00:01",
            ["Tasks:Worker:MetricsSamplerEnabled"] = "false",
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITaskRunStore>(store);
        builder.Services.AddSingleton<ISystemClock>(new FixedClock(Now));
        builder.AddTaskWorkerRuntime();

        using IHost host = builder.Build();
        await host.StartAsync();
        bool retried = await store.WaitForTimeoutScanAttemptsAsync(2, TimeSpan.FromSeconds(2));
        await host.StopAsync();

        Assert.True(retried);
    }

    [Fact]
    public async Task Task_run_scheduler_continues_after_transient_enqueue_failure()
    {
        RecordingTaskRunStore store = new()
        {
            EnqueueFailuresBeforeSuccess = 1,
        };
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tasks:Scheduler:Enabled"] = "true",
            ["Tasks:Scheduler:PollInterval"] = "00:00:00.010",
            ["Tasks:Scheduler:RequestedBy"] = "test-scheduler",
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITaskRunStore>(store);
        builder.Services.AddSingleton<ISystemClock>(new FixedClock(Now));
        builder.Services.AddSingleton<IIdGenerator>(new FixedIdGenerator(RunId));
        builder.Services.AddSingleton<ITaskScheduleProvider>(new SingleScheduleProvider());
        builder.AddTaskRunScheduling();

        using IHost host = builder.Build();
        await host.StartAsync();
        IReadOnlyList<TaskRunRequest> requests = await store.WaitForRequestsAsync(1, TimeSpan.FromSeconds(2));
        await host.StopAsync();

        Assert.True(store.EnqueueAttempts >= 2);
        _ = Assert.Single(requests);
    }

    private sealed record TestPayload(string Name, int Count) : ITaskPayload;

    private sealed class RecordingTaskHandler(RecordingTaskSink sink) : ITaskHandler<TestPayload>
    {
        public Task HandleAsync(
            TestPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken)
        {
            sink.Payload = payload;
            sink.Context = context;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTaskSink
    {
        public TestPayload? Payload { get; set; }
        public TaskExecutionContext? Context { get; set; }
    }

    private sealed class RecordingTaskRunStore : ITaskRunStore
    {
        private readonly List<TaskRunRequest> requests = [];
        private int claimAttempts;
        private int enqueueAttempts;
        private int timeoutScanAttempts;

        public int ClaimFailuresBeforeSuccess { get; init; }
        public int EnqueueFailuresBeforeSuccess { get; init; }
        public int TimeoutScanFailuresBeforeSuccess { get; init; }
        public int EnqueueAttempts => Volatile.Read(ref this.enqueueAttempts);

        public Task EnqueueAsync(TaskRunRequest request, CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref this.enqueueAttempts);
            if (attempt <= this.EnqueueFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Transient enqueue failure.");
            }

            lock (this.requests)
            {
                this.requests.Add(request);
            }

            return Task.CompletedTask;
        }

        public Task<bool> WaitForClaimAttemptsAsync(int expectedCount, TimeSpan timeout) =>
            WaitForCounterAsync(() => Volatile.Read(ref this.claimAttempts), expectedCount, timeout);

        public Task<bool> WaitForTimeoutScanAttemptsAsync(int expectedCount, TimeSpan timeout) =>
            WaitForCounterAsync(() => Volatile.Read(ref this.timeoutScanAttempts), expectedCount, timeout);

        public async Task<IReadOnlyList<TaskRunRequest>> WaitForRequestsAsync(int expectedCount, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
                lock (this.requests)
                {
                    if (this.requests.Count >= expectedCount)
                    {
                        return this.requests.ToArray();
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
            }

            lock (this.requests)
            {
                return this.requests.ToArray();
            }
        }

        public Task<IReadOnlyList<TaskRunSummary>> ListAsync(TaskRunFilter filter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunDetails?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunStats> GetStatsAsync(TaskRunStatsFilter filter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RetryAsync(Guid runId, string? requestedBy, DateTimeOffset scheduledAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
            DateTimeOffset nowUtc,
            TimeSpan staleAfter,
            int maxRuns,
            CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref this.timeoutScanAttempts);
            if (attempt <= this.TimeoutScanFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Transient timeout scan failure.");
            }

            return Task.FromResult<IReadOnlyList<TaskRunSummary>>([]);
        }

        public Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(TaskWorkerClaim claim, CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref this.claimAttempts);
            if (attempt <= this.ClaimFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Transient claim failure.");
            }

            return Task.FromResult<IReadOnlyList<TaskRunLease>>([]);
        }

        public Task MarkStartedAsync(TaskExecutionContext context, DateTimeOffset startedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkSucceededAsync(TaskExecutionContext context, DateTimeOffset completedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkCanceledAsync(TaskExecutionContext context, DateTimeOffset canceledAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkFailedAsync(
            TaskExecutionContext context,
            string error,
            DateTimeOffset failedAtUtc,
            DateTimeOffset? retryAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReportHeartbeatAsync(TaskExecutionContext context, DateTimeOffset observedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReportProgressAsync(
            TaskExecutionContext context,
            TaskProgress progress,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RequestCancellationAsync(Guid runId, string? requestedBy, DateTimeOffset requestedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task EnqueueControlMessageAsync(TaskControlMessage message, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkHandledAsync(TaskExecutionContext context, Guid messageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkFailedAsync(TaskExecutionContext context, Guid messageId, string error, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private static async Task<bool> WaitForCounterAsync(
            Func<int> readCounter,
            int expectedCount,
            TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (readCounter() >= expectedCount)
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
            }

            return readCounter() >= expectedCount;
        }
    }

    private static TaskExecutionContext CreateControlContext() =>
        new(
            RunId,
            "task-samples",
            "slow-report",
            "samples",
            "worker-a",
            "node-a",
            attempt: 1,
            scopeId: "tenant-a");

    private sealed class RecordingControlChannel(IReadOnlyList<TaskControlMessage> messages) : ITaskControlChannel
    {
        public TaskExecutionContext? LastReadContext { get; private set; }
        public int? LastMaxMessages { get; private set; }
        public List<Guid> HandledMessageIds { get; } = [];
        public List<(Guid MessageId, string Error)> FailedMessages { get; } = [];

        public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            this.LastReadContext = context;
            this.LastMaxMessages = maxMessages;
            return Task.FromResult<IReadOnlyList<TaskControlMessage>>(messages);
        }

        public Task MarkHandledAsync(
            TaskExecutionContext context,
            Guid messageId,
            CancellationToken cancellationToken)
        {
            this.HandledMessageIds.Add(messageId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            TaskExecutionContext context,
            Guid messageId,
            string error,
            CancellationToken cancellationToken)
        {
            this.FailedMessages.Add((messageId, error));
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedControlChannel(IReadOnlyList<IReadOnlyList<TaskControlMessage>> batches)
        : ITaskControlChannel
    {
        private int readIndex;

        public int ReadCount { get; private set; }
        public List<Guid> HandledMessageIds { get; } = [];

        public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            this.ReadCount++;
            IReadOnlyList<TaskControlMessage> batch = this.readIndex < batches.Count
                ? batches[this.readIndex]
                : [];
            this.readIndex++;
            return Task.FromResult(batch);
        }

        public Task MarkHandledAsync(
            TaskExecutionContext context,
            Guid messageId,
            CancellationToken cancellationToken)
        {
            this.HandledMessageIds.Add(messageId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            TaskExecutionContext context,
            Guid messageId,
            string error,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class SingleScheduleProvider : ITaskScheduleProvider
    {
        public Task<IReadOnlyList<ScheduledTaskDefinition>> GetSchedulesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScheduledTaskDefinition>>(
            [
                new ScheduledTaskDefinition(
                    "nightly-report",
                    "task-samples",
                    "generate-report",
                    "{}",
                    TimeSpan.FromMinutes(5),
                    "samples",
                    scopeId: "tenant-a",
                    runOnStart: true)
            ]);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class FixedIdGenerator(Guid id) : IIdGenerator
    {
        public Guid NewId() => id;
    }
}

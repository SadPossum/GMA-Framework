namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TaskContractsTests
{
    private static readonly Guid RunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MessageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset EnqueuedAtUtc = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(" Rebuild-Search ", "rebuild-search")]
    [InlineData("sync-catalog-projection", "sync-catalog-projection")]
    public void Task_names_use_kebab_case(string value, string expected)
    {
        Assert.Equal(expected, TaskNames.NormalizeTaskName(value));
    }

    [Theory]
    [InlineData("tasks.cancel-run", "tasks.cancel-run")]
    [InlineData(" Tasks.Pause.Run ", "tasks.pause.run")]
    public void Control_command_names_use_dotted_kebab_codes(string value, string expected)
    {
        Assert.Equal(expected, TaskNames.NormalizeControlCommandName(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("tasks")]
    [InlineData("tasks..cancel")]
    [InlineData("tasks.cancel run")]
    [InlineData("tasks.cancel_run")]
    public void Control_command_names_reject_invalid_shapes(string value)
    {
        Assert.Throws<ArgumentException>(() => TaskNames.NormalizeControlCommandName(value));
    }

    [Fact]
    public void Task_execution_context_normalizes_runtime_identity()
    {
        TaskExecutionContext context = new(
            RunId,
            " Catalog ",
            " Rebuild-Search ",
            " Search-Workers ",
            " Worker-01 ",
            " Node-01 ",
            attempt: 2,
            scopeId: " tenant-a ",
            correlationId: MessageId,
            leaseExtension: TimeSpan.FromMinutes(2));

        Assert.Equal(RunId, context.RunId);
        Assert.Equal("catalog", context.ModuleName);
        Assert.Equal("rebuild-search", context.TaskName);
        Assert.Equal("search-workers", context.WorkerGroup);
        Assert.Equal("worker-01", context.WorkerId);
        Assert.Equal("node-01", context.NodeId);
        Assert.Equal(2, context.Attempt);
        Assert.Equal("tenant-a", context.ScopeId);
        Assert.Equal(MessageId, context.CorrelationId);
        Assert.False(context.CancellationRequested);
        Assert.Equal(TimeSpan.FromMinutes(2), context.LeaseExtension);
    }

    [Fact]
    public void Task_execution_context_rejects_missing_runtime_identity()
    {
        Assert.Throws<ArgumentException>(() => new TaskExecutionContext(
            Guid.Empty,
            "catalog",
            "rebuild-search",
            TaskWorkerGroups.Default,
            "worker-01",
            "node-01",
            attempt: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskExecutionContext(
            RunId,
            "catalog",
            "rebuild-search",
            TaskWorkerGroups.Default,
            "worker-01",
            "node-01",
            attempt: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskExecutionContext(
            RunId,
            "catalog",
            "rebuild-search",
            TaskWorkerGroups.Default,
            "worker-01",
            "node-01",
            attempt: 1,
            leaseExtension: TimeSpan.Zero));
    }

    [Fact]
    public void Task_run_request_normalizes_enqueue_metadata()
    {
        TaskRunRequest request = new(
            RunId,
            " Catalog ",
            " Rebuild-Search ",
            "{\"force\":true}",
            EnqueuedAtUtc,
            EnqueuedAtUtc.AddMinutes(1),
            workerGroup: " Search-Workers ",
            scopeId: " tenant-a ",
            correlationId: MessageId,
            requestedBy: " operator ",
            maxAttempts: 3,
            payloadVersion: 2,
            deduplicationKey: " Import:Tenant-A:Daily ");

        Assert.Equal(RunId, request.RunId);
        Assert.Equal("catalog", request.ModuleName);
        Assert.Equal("rebuild-search", request.TaskName);
        Assert.Equal("search-workers", request.WorkerGroup);
        Assert.Equal("{\"force\":true}", request.PayloadJson);
        Assert.Equal(EnqueuedAtUtc, request.CreatedAtUtc);
        Assert.Equal(EnqueuedAtUtc.AddMinutes(1), request.ScheduledAtUtc);
        Assert.Equal("tenant-a", request.ScopeId);
        Assert.Equal(MessageId, request.CorrelationId);
        Assert.Equal("operator", request.RequestedBy);
        Assert.Equal(3, request.MaxAttempts);
        Assert.Equal(2, request.PayloadVersion);
        Assert.Equal("import:tenant-a:daily", request.DeduplicationKey);
    }

    [Fact]
    public void Task_run_request_rejects_invalid_enqueue_metadata()
    {
        Assert.Throws<ArgumentException>(() => new TaskRunRequest(
            Guid.Empty,
            "catalog",
            "rebuild-search",
            "{}",
            EnqueuedAtUtc,
            EnqueuedAtUtc));
        Assert.Throws<ArgumentException>(() => new TaskRunRequest(
            RunId,
            "catalog",
            "rebuild-search",
            "{}",
            EnqueuedAtUtc,
            EnqueuedAtUtc.AddTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskRunRequest(
            RunId,
            "catalog",
            "rebuild-search",
            "{}",
            EnqueuedAtUtc,
            EnqueuedAtUtc,
            maxAttempts: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskRunRequest(
            RunId,
            "catalog",
            "rebuild-search",
            "{}",
            EnqueuedAtUtc,
            EnqueuedAtUtc,
            payloadVersion: 0));
        Assert.Throws<ArgumentException>(() => new TaskRunRequest(
            RunId,
            new string('a', TaskNames.ModuleNameMaxLength + 1),
            "rebuild-search",
            "{}",
            EnqueuedAtUtc,
            EnqueuedAtUtc));
        Assert.Throws<ArgumentException>(() => new TaskRunRequest(
            RunId,
            "catalog",
            new string('a', TaskNames.TaskNameMaxLength + 1),
            "{}",
            EnqueuedAtUtc,
            EnqueuedAtUtc));
        Assert.Throws<ArgumentException>(() => new TaskRunRequest(
            RunId,
            "catalog",
            "rebuild-search",
            "{}",
            EnqueuedAtUtc,
            EnqueuedAtUtc,
            workerGroup: new string('a', TaskNames.WorkerGroupMaxLength + 1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void Task_run_filter_bounds_skip_count_for_extreme_pages(int pageSize)
    {
        TaskRunFilter filter = new(page: int.MaxValue, pageSize: pageSize);

        Assert.InRange(filter.SkipCount, 0, int.MaxValue);
    }

    [Fact]
    public void Scheduled_task_definition_normalizes_schedule_metadata_and_dedupe_key()
    {
        ScheduledTaskDefinition definition = new(
            " Daily-Import ",
            " Catalog ",
            " Rebuild-Search ",
            "{}",
            TimeSpan.FromMinutes(5),
            " Search-Workers ",
            scopeId: " tenant-a ",
            maxAttempts: 3,
            payloadVersion: 2,
            runOnStart: true);

        Assert.Equal("daily-import", definition.ScheduleName);
        Assert.Equal("catalog", definition.ModuleName);
        Assert.Equal("rebuild-search", definition.TaskName);
        Assert.Equal("search-workers", definition.WorkerGroup);
        Assert.Equal("tenant-a", definition.ScopeId);
        Assert.Equal(3, definition.MaxAttempts);
        Assert.Equal(2, definition.PayloadVersion);
        Assert.True(definition.RunOnStart);
        Assert.Equal(
            "schedule:catalog:rebuild-search:daily-import:v2:20260703120000",
            definition.CreateDeduplicationKey(EnqueuedAtUtc));
    }

    [Fact]
    public void Scheduled_task_definition_default_dedupe_key_includes_task_and_payload_version()
    {
        ScheduledTaskDefinition versionOne = new(
            "daily-import",
            "catalog",
            "rebuild-search",
            "{}",
            TimeSpan.FromMinutes(5),
            payloadVersion: 1);
        ScheduledTaskDefinition versionTwo = new(
            "daily-import",
            "catalog",
            "rebuild-search",
            "{}",
            TimeSpan.FromMinutes(5),
            payloadVersion: 2);

        Assert.NotEqual(
            versionOne.CreateDeduplicationKey(EnqueuedAtUtc),
            versionTwo.CreateDeduplicationKey(EnqueuedAtUtc));
    }

    [Fact]
    public void Scheduled_task_definition_rejects_invalid_schedule_metadata()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScheduledTaskDefinition(
            "daily-import",
            "catalog",
            "rebuild-search",
            "{}",
            TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScheduledTaskDefinition(
            "daily-import",
            "catalog",
            "rebuild-search",
            "{}",
            TimeSpan.FromMinutes(5),
            payloadVersion: 0));
    }

    [Fact]
    public void Task_worker_claim_normalizes_worker_identity_and_lock()
    {
        TaskWorkerClaim claim = new(
            " Search-Workers ",
            " Worker-01 ",
            " Node-01 ",
            EnqueuedAtUtc,
            maxRuns: 10,
            leaseDuration: TimeSpan.FromMinutes(5));

        Assert.Equal("search-workers", claim.WorkerGroup);
        Assert.Equal("worker-01", claim.WorkerId);
        Assert.Equal("node-01", claim.NodeId);
        Assert.Equal(EnqueuedAtUtc, claim.ClaimedAtUtc);
        Assert.Equal(10, claim.MaxRuns);
        Assert.Equal(EnqueuedAtUtc.AddMinutes(5), claim.LockedUntilUtc);
    }

    [Fact]
    public void Task_worker_claim_rejects_invalid_claim_parameters()
    {
        Assert.Throws<ArgumentException>(() => new TaskWorkerClaim(
            "default",
            "worker-01",
            "node-01",
            default,
            maxRuns: 1,
            leaseDuration: TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskWorkerClaim(
            "default",
            "worker-01",
            "node-01",
            EnqueuedAtUtc,
            maxRuns: 0,
            leaseDuration: TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskWorkerClaim(
            "default",
            "worker-01",
            "node-01",
            EnqueuedAtUtc,
            maxRuns: 1,
            leaseDuration: TimeSpan.Zero));
    }

    [Fact]
    public void Task_run_lease_creates_execution_context()
    {
        TaskRunLease lease = new(
            RunId,
            "Catalog",
            "Rebuild-Search",
            "Search-Workers",
            "Worker-01",
            "Node-01",
            "{}",
            attempt: 2,
            EnqueuedAtUtc,
            EnqueuedAtUtc.AddMinutes(5),
            "tenant-a",
            MessageId);

        TaskExecutionContext context = lease.CreateExecutionContext();

        Assert.Equal(RunId, context.RunId);
        Assert.Equal("catalog", context.ModuleName);
        Assert.Equal("rebuild-search", context.TaskName);
        Assert.Equal("search-workers", context.WorkerGroup);
        Assert.Equal("worker-01", context.WorkerId);
        Assert.Equal("node-01", context.NodeId);
        Assert.Equal(2, context.Attempt);
        Assert.Equal("tenant-a", context.ScopeId);
        Assert.Equal(MessageId, context.CorrelationId);
        Assert.False(context.CancellationRequested);
        Assert.Equal(TimeSpan.FromMinutes(5), context.LeaseExtension);
    }

    [Fact]
    public void Task_run_lease_carries_cancellation_request_into_execution_context()
    {
        TaskRunLease lease = new(
            RunId,
            "Catalog",
            "Rebuild-Search",
            "Search-Workers",
            "Worker-01",
            "Node-01",
            "{}",
            attempt: 2,
            EnqueuedAtUtc,
            EnqueuedAtUtc.AddMinutes(5),
            "tenant-a",
            MessageId,
            cancellationRequested: true);

        TaskExecutionContext context = lease.CreateExecutionContext();

        Assert.True(lease.CancellationRequested);
        Assert.True(context.CancellationRequested);
    }

    [Fact]
    public void Task_run_lease_carries_payload_version_into_execution_context()
    {
        TaskRunLease lease = new(
            RunId,
            "Catalog",
            "Rebuild-Search",
            "Search-Workers",
            "Worker-01",
            "Node-01",
            "{}",
            attempt: 2,
            EnqueuedAtUtc,
            EnqueuedAtUtc.AddMinutes(5),
            payloadVersion: 2);

        TaskExecutionContext context = lease.CreateExecutionContext();

        Assert.Equal(2, lease.PayloadVersion);
        Assert.Equal(2, context.PayloadVersion);
    }

    [Fact]
    public void Task_run_lease_rejects_invalid_lock_window()
    {
        Assert.Throws<ArgumentException>(() => new TaskRunLease(
            RunId,
            "catalog",
            "rebuild-search",
            "default",
            "worker-01",
            "node-01",
            "{}",
            attempt: 1,
            EnqueuedAtUtc,
            EnqueuedAtUtc));
    }

    [Fact]
    public void Task_run_status_transitions_identify_terminal_and_claimable_runs()
    {
        DateTimeOffset now = EnqueuedAtUtc.AddMinutes(5);

        Assert.True(TaskRunStatusTransitions.IsTerminal(TaskRunStatus.Succeeded));
        Assert.True(TaskRunStatusTransitions.IsTerminal(TaskRunStatus.Failed));
        Assert.False(TaskRunStatusTransitions.IsTerminal(TaskRunStatus.Running));

        Assert.True(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.Queued,
            EnqueuedAtUtc,
            lockedUntilUtc: null,
            nextAttemptAtUtc: null,
            now));
        Assert.True(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.Running,
            EnqueuedAtUtc,
            lockedUntilUtc: now.AddTicks(-1),
            nextAttemptAtUtc: null,
            now));
        Assert.True(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.RetryScheduled,
            EnqueuedAtUtc,
            lockedUntilUtc: null,
            nextAttemptAtUtc: now,
            now));
        Assert.True(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.CancellationRequested,
            EnqueuedAtUtc,
            lockedUntilUtc: now.AddTicks(-1),
            nextAttemptAtUtc: null,
            now));
    }

    [Fact]
    public void Task_run_status_transitions_reject_unclaimable_runs()
    {
        DateTimeOffset now = EnqueuedAtUtc.AddMinutes(5);

        Assert.False(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.Queued,
            scheduledAtUtc: now.AddTicks(1),
            lockedUntilUtc: null,
            nextAttemptAtUtc: null,
            now));
        Assert.False(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.Leased,
            EnqueuedAtUtc,
            lockedUntilUtc: now.AddTicks(1),
            nextAttemptAtUtc: null,
            now));
        Assert.False(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.CancellationRequested,
            EnqueuedAtUtc,
            lockedUntilUtc: now.AddTicks(1),
            nextAttemptAtUtc: null,
            now));
        Assert.False(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.RetryScheduled,
            EnqueuedAtUtc,
            lockedUntilUtc: null,
            nextAttemptAtUtc: now.AddTicks(1),
            now));
        Assert.False(TaskRunStatusTransitions.CanClaim(
            TaskRunStatus.Succeeded,
            EnqueuedAtUtc,
            lockedUntilUtc: null,
            nextAttemptAtUtc: null,
            now));
    }

    [Theory]
    [InlineData(TaskRunStatus.Leased, true)]
    [InlineData(TaskRunStatus.Running, false)]
    public void Task_run_status_transitions_identify_startable_runs(TaskRunStatus status, bool expected)
    {
        Assert.Equal(expected, TaskRunStatusTransitions.CanStart(status));
    }

    [Theory]
    [InlineData(TaskRunStatus.Running, true)]
    [InlineData(TaskRunStatus.CancellationRequested, true)]
    [InlineData(TaskRunStatus.Queued, false)]
    public void Task_run_status_transitions_identify_completable_runs(TaskRunStatus status, bool expected)
    {
        Assert.Equal(expected, TaskRunStatusTransitions.CanComplete(status));
    }

    [Fact]
    public void Task_run_status_transitions_reject_unknown_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskRunStatusTransitions.RequireKnown(TaskRunStatus.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskRunStatusTransitions.RequireKnown((TaskRunStatus)42));
    }

    [Theory]
    [InlineData(TaskRunStatus.Queued, "queued")]
    [InlineData(TaskRunStatus.Leased, "leased")]
    [InlineData(TaskRunStatus.Running, "running")]
    [InlineData(TaskRunStatus.WaitingForControl, "waiting-for-control")]
    [InlineData(TaskRunStatus.RetryScheduled, "retry-scheduled")]
    [InlineData(TaskRunStatus.Succeeded, "succeeded")]
    [InlineData(TaskRunStatus.Failed, "failed")]
    [InlineData(TaskRunStatus.CancellationRequested, "cancellation-requested")]
    [InlineData(TaskRunStatus.Canceled, "canceled")]
    [InlineData(TaskRunStatus.TimedOut, "timed-out")]
    public void Task_run_status_names_use_stable_wire_names(TaskRunStatus status, string expected)
    {
        Assert.Equal(expected, TaskRunStatusNames.ToWireName(status));
    }

    [Theory]
    [InlineData("queued", TaskRunStatus.Queued)]
    [InlineData("RetryScheduled", TaskRunStatus.RetryScheduled)]
    [InlineData("retry-scheduled", TaskRunStatus.RetryScheduled)]
    [InlineData("WaitingForControl", TaskRunStatus.WaitingForControl)]
    [InlineData("waiting-for-control", TaskRunStatus.WaitingForControl)]
    [InlineData("CancellationRequested", TaskRunStatus.CancellationRequested)]
    [InlineData("cancellation-requested", TaskRunStatus.CancellationRequested)]
    [InlineData("cancelled", TaskRunStatus.Canceled)]
    public void Task_run_status_names_parse_wire_and_enum_names(string value, TaskRunStatus expected)
    {
        Assert.True(TaskRunStatusNames.TryParse(value, out TaskRunStatus actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("not-a-status")]
    public void Task_run_status_names_reject_unknown_or_empty_values(string value)
    {
        Assert.False(TaskRunStatusNames.TryParse(value, out TaskRunStatus status));
        Assert.Equal(TaskRunStatus.Unknown, status);
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskRunStatusNames.ToWireName(status));
    }

    [Fact]
    public void Task_run_status_names_parse_optional_filters()
    {
        Assert.True(TaskRunStatusNames.TryParseOptional(null, out TaskRunStatus? missing));
        Assert.Null(missing);
        Assert.True(TaskRunStatusNames.TryParseOptional(" ", out TaskRunStatus? empty));
        Assert.Null(empty);
        Assert.True(TaskRunStatusNames.TryParseOptional("retry-scheduled", out TaskRunStatus? parsed));
        Assert.Equal(TaskRunStatus.RetryScheduled, parsed);
        Assert.False(TaskRunStatusNames.TryParseOptional("unknown", out TaskRunStatus? invalid));
        Assert.Null(invalid);
    }

    [Fact]
    public void Task_control_message_status_transitions_reject_unknown_status()
    {
        Assert.True(TaskControlMessageStatusTransitions.IsReadable(TaskControlMessageStatus.Pending));
        Assert.True(TaskControlMessageStatusTransitions.CanMarkHandled(TaskControlMessageStatus.Failed));
        Assert.False(TaskControlMessageStatusTransitions.IsReadable(TaskControlMessageStatus.Handled));
        Assert.False(TaskControlMessageStatusTransitions.CanMarkFailed(TaskControlMessageStatus.Expired));
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskControlMessageStatusTransitions.RequireKnown(TaskControlMessageStatus.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskControlMessageStatusTransitions.RequireKnown((TaskControlMessageStatus)42));
    }

    [Fact]
    public void Task_handler_registration_normalizes_metadata()
    {
        TaskHandlerRegistration registration = TaskHandlerRegistration.Create<TestTaskPayload, TestTaskHandler>(
            " Catalog ",
            " Rebuild-Search ",
            " Search-Workers ",
            payloadVersion: 2,
            kind: ModuleTaskKind.Daemon,
            supportsControlMessages: true,
            metadata: [TenantScopeMetadataItem.Instance]);

        Assert.Equal("catalog", registration.ModuleName);
        Assert.Equal("rebuild-search", registration.TaskName);
        Assert.Equal("search-workers", registration.WorkerGroup);
        Assert.Equal(typeof(TestTaskPayload), registration.PayloadType);
        Assert.Equal(typeof(TestTaskHandler), registration.HandlerType);
        Assert.Equal(ModuleTaskKind.Daemon, registration.Kind);
        Assert.True(registration.IsTenantScoped());
        Assert.Equal(2, registration.PayloadVersion);
        Assert.True(registration.SupportsControlMessages);
    }

    [Fact]
    public void Task_handler_registration_can_use_payload_attribute_metadata()
    {
        TaskHandlerRegistration registration =
            TaskHandlerRegistration.Create<AttributedTaskPayload, AttributedTaskHandler>("catalog");

        Assert.Equal("catalog", registration.ModuleName);
        Assert.Equal("rebuild-search", registration.TaskName);
        Assert.Equal("search-workers", registration.WorkerGroup);
        Assert.Equal(typeof(AttributedTaskPayload), registration.PayloadType);
        Assert.Equal(typeof(AttributedTaskHandler), registration.HandlerType);
        Assert.Equal(ModuleTaskKind.Daemon, registration.Kind);
        Assert.True(registration.IsTenantScoped());
        Assert.Equal(2, registration.PayloadVersion);
        Assert.True(registration.SupportsControlMessages);
    }

    [Fact]
    public void Task_handler_service_registration_can_use_payload_attribute_metadata()
    {
        ServiceCollection services = new();

        services.AddTaskHandler<AttributedTaskPayload, AttributedTaskHandler>("catalog");
        services.AddTaskHandler<AttributedTaskPayload, AttributedTaskHandler>("catalog");

        TaskHandlerRegistration registration = Assert.IsType<TaskHandlerRegistration>(Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(TaskHandlerRegistration)).ImplementationInstance);
        Assert.Equal("catalog", registration.ModuleName);
        Assert.Equal("rebuild-search", registration.TaskName);
        Assert.Equal(2, registration.PayloadVersion);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AttributedTaskHandler));
    }

    [Fact]
    public void Task_handler_attribute_registration_requires_payload_metadata()
    {
        ServiceCollection services = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddTaskHandler<TestTaskPayload, TestTaskHandler>("catalog"));

        Assert.Contains(nameof(TaskNameAttribute), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Task_handler_attribute_registration_requires_payload_version_metadata()
    {
        ServiceCollection services = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddTaskHandler<MissingVersionTaskPayload, MissingVersionTaskHandler>("catalog"));

        Assert.Contains(nameof(TaskPayloadVersionAttribute), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Task_payload_version_attribute_rejects_non_positive_versions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskPayloadVersionAttribute(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskPayloadVersionAttribute(-1));
    }

    [Fact]
    public void Task_handler_registration_rejects_unknown_kind()
    {
        Assert.Throws<ArgumentException>(() => TaskHandlerRegistration.Create<TestTaskPayload, TestTaskHandler>(
            "catalog",
            "rebuild-search",
            kind: ModuleTaskKind.Unknown));
    }

    [Fact]
    public void Task_handler_registry_rejects_duplicate_task_handlers()
    {
        TaskHandlerRegistration first = TaskHandlerRegistration.Create<TestTaskPayload, TestTaskHandler>(
            "catalog",
            "rebuild-search");
        TaskHandlerRegistration duplicate = TaskHandlerRegistration.Create<OtherTaskPayload, OtherTaskHandler>(
            "catalog",
            "rebuild-search");

        Assert.Throws<InvalidOperationException>(() => new TaskHandlerRegistry([first, duplicate]));
    }

    [Fact]
    public void Task_handler_registry_allows_same_task_name_with_different_payload_versions()
    {
        TaskHandlerRegistration first = TaskHandlerRegistration.Create<TestTaskPayload, TestTaskHandler>(
            "catalog",
            "rebuild-search",
            payloadVersion: 1);
        TaskHandlerRegistration second = TaskHandlerRegistration.Create<OtherTaskPayload, OtherTaskHandler>(
            "catalog",
            "rebuild-search",
            payloadVersion: 2);

        TaskHandlerRegistry registry = new([first, second]);

        Assert.Same(first, registry.Find("catalog", "rebuild-search", payloadVersion: 1));
        Assert.Same(second, registry.Find("catalog", "rebuild-search", payloadVersion: 2));
    }

    [Fact]
    public void Task_handler_service_registration_is_idempotent_for_same_metadata()
    {
        ServiceCollection services = new();

        services.AddTaskHandler<TestTaskPayload, TestTaskHandler>("catalog", "rebuild-search");
        services.AddTaskHandler<TestTaskPayload, TestTaskHandler>("catalog", "rebuild-search");

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TaskHandlerRegistration));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITaskHandlerRegistry));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TestTaskHandler));
    }

    [Fact]
    public void Task_handler_service_registration_rejects_same_identity_with_different_metadata()
    {
        ServiceCollection services = new();

        services.AddTaskHandler<TestTaskPayload, TestTaskHandler>("catalog", "rebuild-search");

        Assert.Throws<InvalidOperationException>(() => services.AddTaskHandler<TestTaskPayload, TestTaskHandler>(
            "catalog",
            "rebuild-search",
            supportsControlMessages: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(100)]
    public void Task_progress_accepts_percent_range(int percentComplete)
    {
        TaskProgress progress = new(percentComplete, " Working ");

        Assert.Equal(percentComplete, progress.PercentComplete);
        Assert.Equal("Working", progress.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Task_progress_rejects_invalid_percent(int percentComplete)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskProgress(percentComplete));
    }

    [Fact]
    public void Task_control_message_normalizes_contract_fields()
    {
        TaskControlMessage message = new(
            MessageId,
            RunId,
            " Tasks.Cancel-Run ",
            "{}",
            EnqueuedAtUtc,
            " operator ",
            EnqueuedAtUtc.AddMinutes(5));

        Assert.Equal(MessageId, message.MessageId);
        Assert.Equal(RunId, message.RunId);
        Assert.Equal("tasks.cancel-run", message.CommandName);
        Assert.Equal("{}", message.PayloadJson);
        Assert.Equal("operator", message.RequestedBy);
        Assert.Equal(EnqueuedAtUtc.AddMinutes(5), message.ExpiresAtUtc);
    }

    [Fact]
    public void Task_control_message_rejects_invalid_ids_or_expiration()
    {
        Assert.Throws<ArgumentException>(() => new TaskControlMessage(
            Guid.Empty,
            RunId,
            "tasks.cancel-run",
            "{}",
            EnqueuedAtUtc));
        Assert.Throws<ArgumentException>(() => new TaskControlMessage(
            MessageId,
            RunId,
            "tasks.cancel-run",
            "{}",
            default));
        Assert.Throws<ArgumentException>(() => new TaskControlMessage(
            MessageId,
            RunId,
            "tasks.cancel-run",
            "{}",
            EnqueuedAtUtc,
            expiresAtUtc: EnqueuedAtUtc));
    }

    private sealed record TestTaskPayload : ITaskPayload;

    private sealed class TestTaskHandler : ITaskHandler<TestTaskPayload>
    {
        public Task HandleAsync(
            TestTaskPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [TaskName(TaskName)]
    [TaskPayloadVersion(PayloadVersion)]
    [TaskDescription("Rebuild search index.")]
    [TaskKind(ModuleTaskKind.Daemon)]
    [TaskWorkerGroup("search-workers")]
    [SupportsTaskControl]
    [TenantScoped]
    private sealed record AttributedTaskPayload : ITaskPayload
    {
        public const string TaskName = "rebuild-search";
        public const int PayloadVersion = 2;
    }

    private sealed class AttributedTaskHandler : ITaskHandler<AttributedTaskPayload>
    {
        public Task HandleAsync(
            AttributedTaskPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [TaskName(TaskName)]
    [TaskDescription("Missing payload version.")]
    [TaskKind(ModuleTaskKind.OneShot)]
    private sealed record MissingVersionTaskPayload : ITaskPayload
    {
        public const string TaskName = "missing-version";
    }

    private sealed class MissingVersionTaskHandler : ITaskHandler<MissingVersionTaskPayload>
    {
        public Task HandleAsync(
            MissingVersionTaskPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed record OtherTaskPayload : ITaskPayload;

    private sealed class OtherTaskHandler : ITaskHandler<OtherTaskPayload>
    {
        public Task HandleAsync(
            OtherTaskPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

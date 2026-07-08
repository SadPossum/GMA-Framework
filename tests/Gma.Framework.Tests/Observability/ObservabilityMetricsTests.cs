namespace Gma.Framework.Tests;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.Runtime;
using Gma.Framework.Tasks;
using Gma.Framework.Results;
using Gma.Framework.Caching.Infrastructure;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Tasks.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
[Collection(MetricsTestGroupDefinition.Name)]
public sealed class ObservabilityMetricsTests
{
    [Fact]
    public async Task Command_behavior_records_bounded_module_operation_and_result_tags()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingCommandBehavior<TestCommand, Unit> behavior = new(
            NullLogger<LoggingCommandBehavior<TestCommand, Unit>>.Instance,
            metrics);

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        MetricMeasurement executed = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.CommandsExecuted);
        Assert.Equal(1, executed.Value);
        Assert.Equal("framework", executed.Tags[ObservabilityTagNames.Module]);
        Assert.Equal(nameof(TestCommand), executed.Tags[ObservabilityTagNames.Operation]);
        Assert.Equal("success", executed.Tags[ObservabilityTagNames.Result]);
        Assert.DoesNotContain(executed.Tags.Keys, key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.CommandsDuration && item.Value >= 0);
    }

    [Fact]
    public async Task Command_behavior_records_error_code_for_expected_failure()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingCommandBehavior<TestCommand, Unit> behavior = new(
            NullLogger<LoggingCommandBehavior<TestCommand, Unit>>.Instance,
            metrics);
        Error error = new("Test.Failed", "Expected failure.");

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(Result.Failure<Unit>(error)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        MetricMeasurement executed = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.CommandsExecuted);
        Assert.Equal("failure", executed.Tags[ObservabilityTagNames.Result]);
        Assert.Equal(error.Code, executed.Tags[ObservabilityTagNames.ErrorCode]);
    }

    [Fact]
    public async Task Application_metrics_use_configured_application_namespace()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(
            measurements,
            ObservabilityMeterNames.ApplicationFor("acme-orders"));
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = new(
            provider.GetRequiredService<IMeterFactory>(),
            ApplicationIdentity("acme-orders"));

        metrics.Record("auth", nameof(TestCommand), isSuccess: true, null, TimeSpan.FromMilliseconds(1));

        MetricMeasurement executed = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.CommandsExecutedFor("acme-orders"));
        Assert.Equal(1, executed.Value);
    }

    [Fact]
    public async Task Query_behavior_records_bounded_module_operation_and_result_tags()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        QueryMetrics metrics = CreateQueryMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingQueryBehavior<TestQuery, Unit> behavior = new(
            NullLogger<LoggingQueryBehavior<TestQuery, Unit>>.Instance,
            metrics);

        Result<Unit> result = await behavior.HandleAsync(
            new TestQuery(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        MetricMeasurement executed = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.QueriesExecuted);
        Assert.Equal(1, executed.Value);
        Assert.Equal("framework", executed.Tags[ObservabilityTagNames.Module]);
        Assert.Equal(nameof(TestQuery), executed.Tags[ObservabilityTagNames.Operation]);
        Assert.Equal("success", executed.Tags[ObservabilityTagNames.Result]);
        Assert.DoesNotContain(executed.Tags.Keys, key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.QueriesDuration && item.Value >= 0);
    }

    [Fact]
    public async Task Query_behavior_records_error_code_for_expected_failure()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        QueryMetrics metrics = CreateQueryMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingQueryBehavior<TestQuery, Unit> behavior = new(
            NullLogger<LoggingQueryBehavior<TestQuery, Unit>>.Instance,
            metrics);
        Error error = new("Test.Failed", "Expected failure.");

        Result<Unit> result = await behavior.HandleAsync(
            new TestQuery(),
            () => Task.FromResult(Result.Failure<Unit>(error)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        MetricMeasurement executed = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.QueriesExecuted);
        Assert.Equal("failure", executed.Tags[ObservabilityTagNames.Result]);
        Assert.Equal(error.Code, executed.Tags[ObservabilityTagNames.ErrorCode]);
    }

    [Fact]
    public async Task Outbox_metrics_record_claimed_count_with_bounded_tags()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements, ObservabilityMeterNames.Messaging);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxMetrics metrics = CreateOutboxMetrics(provider.GetRequiredService<IMeterFactory>());

        metrics.RecordClaimed("auth", 3);
        metrics.RecordClaimed("catalog", 0);

        MetricMeasurement claimed = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.OutboxClaimed);
        Assert.Equal(3, claimed.Value);
        Assert.Equal("auth", claimed.Tags[ObservabilityTagNames.Module]);
        Assert.DoesNotContain(claimed.Tags.Keys, key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(claimed.Tags.Keys, key => key.Contains("message", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Outbox_metrics_record_publish_attempts_with_bounded_tags()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements, ObservabilityMeterNames.Messaging);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxMetrics metrics = CreateOutboxMetrics(provider.GetRequiredService<IMeterFactory>());
        const string subject = "gma.auth.member-registered.v1";

        metrics.RecordPublished("auth", subject, TimeSpan.FromMilliseconds(12));
        metrics.RecordFailed("auth", subject, TimeSpan.FromMilliseconds(20));

        MetricMeasurement published = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.OutboxPublished);
        MetricMeasurement failed = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.OutboxFailed);
        MetricMeasurement[] durations = measurements
            .Where(item => item.InstrumentName == ObservabilityInstrumentNames.OutboxPublishDuration)
            .ToArray();

        Assert.Equal(1, published.Value);
        Assert.Equal("auth", published.Tags[ObservabilityTagNames.Module]);
        Assert.Equal(subject, published.Tags[ObservabilityTagNames.Subject]);
        Assert.Equal("success", published.Tags[ObservabilityTagNames.Result]);
        Assert.Equal(1, failed.Value);
        Assert.Equal("failure", failed.Tags[ObservabilityTagNames.Result]);
        Assert.All(durations, duration =>
        {
            Assert.Equal("auth", duration.Tags[ObservabilityTagNames.Module]);
            Assert.Equal(subject, duration.Tags[ObservabilityTagNames.Subject]);
            string result = Assert.IsType<string>(duration.Tags[ObservabilityTagNames.Result]);
            Assert.True(result is "success" or "failure");
        });
        Assert.Equal(2, durations.Length);
    }

    [Fact]
    public async Task Inbox_metrics_record_process_outcomes_with_bounded_tags()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements, ObservabilityMeterNames.Messaging);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        InboxMetrics metrics = CreateInboxMetrics(provider.GetRequiredService<IMeterFactory>());
        const string subject = "gma.catalog.item-created.v1";

        metrics.RecordProcessed(
            "ordering",
            "catalog-item-created-projection",
            subject,
            InboxProcessStatus.Processed,
            TimeSpan.FromMilliseconds(15));

        MetricMeasurement messages = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.InboxMessages);
        MetricMeasurement duration = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.InboxProcessDuration);

        Assert.Equal(1, messages.Value);
        Assert.Equal("ordering", messages.Tags[ObservabilityTagNames.Module]);
        Assert.Equal("catalog-item-created-projection", messages.Tags[ObservabilityTagNames.Operation]);
        Assert.Equal(subject, messages.Tags[ObservabilityTagNames.Subject]);
        Assert.Equal("processed", messages.Tags[ObservabilityTagNames.Result]);
        Assert.Equal(messages.Tags[ObservabilityTagNames.Module], duration.Tags[ObservabilityTagNames.Module]);
        Assert.Equal(messages.Tags[ObservabilityTagNames.Operation], duration.Tags[ObservabilityTagNames.Operation]);
        Assert.Equal(messages.Tags[ObservabilityTagNames.Subject], duration.Tags[ObservabilityTagNames.Subject]);
        Assert.DoesNotContain(messages.Tags.Keys, key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(messages.Tags.Keys, key => key.Contains("event", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Inbox_metrics_map_unknown_status_to_bounded_result_tag()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements, ObservabilityMeterNames.Messaging);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        InboxMetrics metrics = CreateInboxMetrics(provider.GetRequiredService<IMeterFactory>());

        metrics.RecordProcessed(
            "ordering",
            "catalog-item-created-projection",
            "gma.catalog.item-created.v1",
            InboxProcessStatus.Unknown,
            TimeSpan.FromMilliseconds(5));

        MetricMeasurement messages = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.InboxMessages);

        Assert.Equal("unknown", messages.Tags[ObservabilityTagNames.Result]);
    }

    [Fact]
    public async Task Messaging_metrics_normalize_module_and_subject_tags()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements, ObservabilityMeterNames.Messaging);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        OutboxMetrics metrics = CreateOutboxMetrics(provider.GetRequiredService<IMeterFactory>());

        metrics.RecordPublished(
            " Auth ",
            " GMA.Auth.Member-Registered.V1 ",
            TimeSpan.FromMilliseconds(5));

        MetricMeasurement published = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.OutboxPublished);

        Assert.Equal("auth", published.Tags[ObservabilityTagNames.Module]);
        Assert.Equal("gma.auth.member-registered.v1", published.Tags[ObservabilityTagNames.Subject]);
    }

    [Fact]
    public async Task Cache_metrics_normalize_provider_and_map_unknown_results()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements, ObservabilityMeterNames.Caching);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CacheMetrics metrics = CreateCacheMetrics(provider.GetRequiredService<IMeterFactory>());

        metrics.RecordRequest(
            " Catalog ",
            "Redis",
            "unexpected-cardinality",
            TimeSpan.FromMilliseconds(7));

        MetricMeasurement request = Assert.Single(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.CacheRequests);

        Assert.Equal("catalog", request.Tags[ObservabilityTagNames.Module]);
        Assert.Equal("redis", request.Tags[ObservabilityTagNames.Provider]);
        Assert.Equal("unknown", request.Tags[ObservabilityTagNames.Result]);
    }

    [Fact]
    public async Task Task_metrics_observe_queue_depth_and_active_run_gauges_with_bounded_status_tags()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements, ObservabilityMeterNames.Tasks);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        TaskMetrics metrics = new(
            provider.GetRequiredService<IMeterFactory>(),
            new TaskMetricsSnapshotStore(),
            ApplicationIdentity());

        metrics.RecordSnapshot(new TaskRunStats(
        [
            new TaskRunStatusCount(TaskRunStatus.Queued, 2),
            new TaskRunStatusCount(TaskRunStatus.RetryScheduled, 1),
            new TaskRunStatusCount(TaskRunStatus.Leased, 3),
            new TaskRunStatusCount(TaskRunStatus.Running, 4),
            new TaskRunStatusCount(TaskRunStatus.CancellationRequested, 5)
        ]));
        listener.RecordObservableInstruments();

        MetricMeasurement[] queueDepth = measurements
            .Where(item => item.InstrumentName == ObservabilityInstrumentNames.TaskQueueDepth)
            .OrderBy(item => item.Tags[ObservabilityTagNames.TaskStatus])
            .ToArray();
        MetricMeasurement[] activeRuns = measurements
            .Where(item => item.InstrumentName == ObservabilityInstrumentNames.TaskActiveRuns)
            .OrderBy(item => item.Tags[ObservabilityTagNames.TaskStatus])
            .ToArray();

        Assert.Equal(2, queueDepth.Length);
        Assert.Contains(queueDepth, item =>
            item.Value == 2 && Equals(item.Tags[ObservabilityTagNames.TaskStatus], "queued"));
        Assert.Contains(queueDepth, item =>
            item.Value == 1 && Equals(item.Tags[ObservabilityTagNames.TaskStatus], "retry-scheduled"));
        Assert.Equal(3, activeRuns.Length);
        Assert.Contains(activeRuns, item =>
            item.Value == 3 && Equals(item.Tags[ObservabilityTagNames.TaskStatus], "leased"));
        Assert.Contains(activeRuns, item =>
            item.Value == 4 && Equals(item.Tags[ObservabilityTagNames.TaskStatus], "running"));
        Assert.Contains(activeRuns, item =>
            item.Value == 5 && Equals(item.Tags[ObservabilityTagNames.TaskStatus], "cancellation-requested"));
        Assert.All(queueDepth.Concat(activeRuns), item =>
            Assert.DoesNotContain(item.Tags.Keys, key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Metrics_reject_malformed_direct_tag_inputs()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics commandMetrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        OutboxMetrics outboxMetrics = CreateOutboxMetrics(provider.GetRequiredService<IMeterFactory>());

        Assert.Throws<ArgumentException>(() =>
            commandMetrics.Record("auth module", nameof(TestCommand), isSuccess: true, null, TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() =>
            commandMetrics.Record("auth", "bad operation", isSuccess: true, null, TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() =>
            outboxMetrics.RecordPublished("auth", "auth.member-registered", TimeSpan.Zero));
    }

    [Fact]
    public async Task Command_behavior_succeeds_when_logger_throws()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingCommandBehavior<TestCommand, Unit> behavior = new(
            new ThrowingLogger<LoggingCommandBehavior<TestCommand, Unit>>(),
            metrics);

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.CommandsExecuted);
    }

    [Fact]
    public async Task Query_behavior_succeeds_when_logger_throws()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        QueryMetrics metrics = CreateQueryMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingQueryBehavior<TestQuery, Unit> behavior = new(
            new ThrowingLogger<LoggingQueryBehavior<TestQuery, Unit>>(),
            metrics);

        Result<Unit> result = await behavior.HandleAsync(
            new TestQuery(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(
            measurements,
            item => item.InstrumentName == ObservabilityInstrumentNames.QueriesExecuted);
    }

    [Fact]
    public async Task Command_behavior_succeeds_when_metric_listener_throws()
    {
        using MeterListener listener = CreateThrowingApplicationMeterListener();
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingCommandBehavior<TestCommand, Unit> behavior = new(
            NullLogger<LoggingCommandBehavior<TestCommand, Unit>>.Instance,
            metrics);

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Query_behavior_succeeds_when_metric_listener_throws()
    {
        using MeterListener listener = CreateThrowingApplicationMeterListener();
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        QueryMetrics metrics = CreateQueryMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingQueryBehavior<TestQuery, Unit> behavior = new(
            NullLogger<LoggingQueryBehavior<TestQuery, Unit>>.Instance,
            metrics);

        Result<Unit> result = await behavior.HandleAsync(
            new TestQuery(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Command_behavior_accepts_optional_scope_contributors()
    {
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        CapturingLogger<LoggingCommandBehavior<TestCommand, Unit>> logger = new();
        LoggingCommandBehavior<TestCommand, Unit> behavior = new(
            logger,
            metrics,
            [new TestScopeContributor("cqrs.scope", "command")]);

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        IReadOnlyDictionary<string, object?> scope = Assert.Single(logger.Scopes);
        Assert.Equal("command", scope["cqrs.scope"]);
        Assert.Equal("framework", scope[ObservabilityLogPropertyNames.Module]);
        Assert.Equal(nameof(TestCommand), scope[ObservabilityLogPropertyNames.Operation]);
    }

    [Fact]
    public async Task Query_behavior_ignores_scope_contributor_failures()
    {
        ThrowingScopeContributor.SeenRequestKinds.Clear();
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        QueryMetrics metrics = CreateQueryMetrics(provider.GetRequiredService<IMeterFactory>());
        CapturingLogger<LoggingQueryBehavior<TestQuery, Unit>> logger = new();
        LoggingQueryBehavior<TestQuery, Unit> behavior = new(
            logger,
            metrics,
            [new ThrowingScopeContributor(), new TestScopeContributor("cqrs.scope", "query")]);

        Result<Unit> result = await behavior.HandleAsync(
            new TestQuery(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        IReadOnlyDictionary<string, object?> scope = Assert.Single(logger.Scopes);
        Assert.Equal("query", scope["cqrs.scope"]);
        Assert.Equal(CqrsRequestKind.Query, Assert.Single(ThrowingScopeContributor.SeenRequestKinds));
    }

    [Fact]
    public async Task Command_behavior_preserves_original_exception_when_logger_throws()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingCommandBehavior<TestCommand, Unit> behavior = new(
            new ThrowingLogger<LoggingCommandBehavior<TestCommand, Unit>>(),
            metrics);
        InvalidOperationException expected = new("handler failed");

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(
                new TestCommand(),
                () => throw expected,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Query_behavior_preserves_original_exception_when_logger_throws()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        QueryMetrics metrics = CreateQueryMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingQueryBehavior<TestQuery, Unit> behavior = new(
            new ThrowingLogger<LoggingQueryBehavior<TestQuery, Unit>>(),
            metrics);
        InvalidOperationException expected = new("handler failed");

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(
                new TestQuery(),
                () => throw expected,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Command_behavior_preserves_original_exception_when_metric_listener_throws()
    {
        using MeterListener listener = CreateThrowingApplicationMeterListener();
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CommandMetrics metrics = CreateCommandMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingCommandBehavior<TestCommand, Unit> behavior = new(
            NullLogger<LoggingCommandBehavior<TestCommand, Unit>>.Instance,
            metrics);
        InvalidOperationException expected = new("handler failed");

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(
                new TestCommand(),
                () => throw expected,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Query_behavior_preserves_original_exception_when_metric_listener_throws()
    {
        using MeterListener listener = CreateThrowingApplicationMeterListener();
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        QueryMetrics metrics = CreateQueryMetrics(provider.GetRequiredService<IMeterFactory>());
        LoggingQueryBehavior<TestQuery, Unit> behavior = new(
            NullLogger<LoggingQueryBehavior<TestQuery, Unit>>.Instance,
            metrics);
        InvalidOperationException expected = new("handler failed");

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(
                new TestQuery(),
                () => throw expected,
                CancellationToken.None));

        Assert.Same(expected, actual);
    }

    private static CommandMetrics CreateCommandMetrics(IMeterFactory meterFactory) =>
        new(meterFactory, ApplicationIdentity());

    private static QueryMetrics CreateQueryMetrics(IMeterFactory meterFactory) =>
        new(meterFactory, ApplicationIdentity());

    private static OutboxMetrics CreateOutboxMetrics(IMeterFactory meterFactory) =>
        new(meterFactory, ApplicationIdentity());

    private static InboxMetrics CreateInboxMetrics(IMeterFactory meterFactory) =>
        new(meterFactory, ApplicationIdentity());

    private static CacheMetrics CreateCacheMetrics(IMeterFactory meterFactory) =>
        new(meterFactory, ApplicationIdentity());

    private static IOptions<ApplicationIdentityOptions> ApplicationIdentity(
        string applicationNamespace = "gma") =>
        Options.Create(new ApplicationIdentityOptions { Namespace = applicationNamespace });

    private static MeterListener CreateListener(
        ICollection<MetricMeasurement> measurements,
        params string[] meterNames)
    {
        string[] enabledMeters = meterNames.Length == 0
            ? [ObservabilityMeterNames.Application]
            : meterNames;
        MeterListener listener = new()
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (enabledMeters.Contains(instrument.Meter.Name, StringComparer.Ordinal))
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new MetricMeasurement(instrument.Name, value, ToDictionary(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new MetricMeasurement(instrument.Name, value, ToDictionary(tags))));
        listener.Start();

        return listener;
    }

    private static MeterListener CreateThrowingApplicationMeterListener()
    {
        MeterListener listener = new()
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == ObservabilityMeterNames.Application)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
            throw new InvalidOperationException("Metric listener unavailable."));
        listener.SetMeasurementEventCallback<double>((_, _, _, _) =>
            throw new InvalidOperationException("Metric listener unavailable."));
        listener.Start();

        return listener;
    }

    private static Dictionary<string, object?> ToDictionary(
        ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        tags.ToArray().ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private sealed record TestCommand : ICommand<Unit>;

    private sealed record TestQuery : IQuery<Unit>;

    private sealed record MetricMeasurement(
        string InstrumentName,
        double Value,
        Dictionary<string, object?> Tags);

    private sealed class TestScopeContributor(string key, object? value) : ICqrsLogScopeContributor
    {
        public void Enrich(CqrsLogScopeContext context, IDictionary<string, object?> scopeProperties)
        {
            ArgumentNullException.ThrowIfNull(context);
            scopeProperties[key] = value;
        }
    }

    private sealed class ThrowingScopeContributor : ICqrsLogScopeContributor
    {
        public static List<CqrsRequestKind> SeenRequestKinds { get; } = [];

        public void Enrich(CqrsLogScopeContext context, IDictionary<string, object?> scopeProperties)
        {
            SeenRequestKinds.Add(context.RequestKind);
            throw new InvalidOperationException("Scope contributor unavailable.");
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<IReadOnlyDictionary<string, object?>> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                this.Scopes.Add(properties.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
            }

            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        private NullScope()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            new ThrowingScope();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }

    private sealed class ThrowingScope : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("Scope unavailable.");
    }
}

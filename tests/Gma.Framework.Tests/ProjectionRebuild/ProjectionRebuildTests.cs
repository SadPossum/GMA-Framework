namespace Gma.Framework.Tests;

using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Naming;
using Gma.Framework.Observability;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Xunit;

[Trait("Category", "Unit")]
[Collection(MetricsTestGroupDefinition.Name)]
public sealed class ProjectionRebuildTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid RunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Contracts_validate_counts_cursors_tenant_and_batch_size()
    {
        ProjectionRebuildRequest request = new(
            " Catalog-Projection ",
            projectionVersion: 2,
            batchSize: 5,
            dryRun: true,
            cursor: " cursor-1 ");
        ProjectionRebuildCheckpointKey key = new(
            " Ordering ",
            RunId,
            " Catalog-Projection ",
            " tenant-a ");
        ProjectionRebuildCheckpoint checkpoint = ProjectionRebuildCheckpoint
            .Start(2, Now, " cursor-1 ")
            .Advance(" cursor-2 ", 3, new ProjectionWriteResult(2, skippedCount: 1), Now.AddSeconds(1))
            .Complete(Now.AddSeconds(2));

        Assert.Equal("catalog-projection", request.ProjectionName);
        Assert.Equal(2, request.ProjectionVersion);
        Assert.Equal(5, request.BatchSize);
        Assert.True(request.DryRun);
        Assert.Equal("cursor-1", request.Cursor);
        Assert.Equal("ordering", key.ModuleName);
        Assert.Equal("catalog-projection", key.ProjectionName);
        Assert.Equal("tenant-a", key.ScopeId);
        Assert.True(checkpoint.IsCompleted);
        Assert.Equal("cursor-2", checkpoint.Cursor);
        Assert.Equal(3, checkpoint.ProcessedCount);
        Assert.Equal(2, checkpoint.WrittenCount);
        Assert.Equal(1, checkpoint.SkippedCount);
        Assert.Equal(Now.AddSeconds(2), checkpoint.CompletedAtUtc);

        Assert.Throws<ArgumentException>(() => new ProjectionReadBatch<string>(["a"], null, hasMore: true));
        Assert.Throws<ArgumentException>(() => new ProjectionReadBatch<string>(["a"], "\u0001", hasMore: false));
        Assert.Throws<ArgumentException>(() => new ProjectionRebuildRequest("bad name", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionRebuildRequest("catalog-projection", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionRebuildRequest("catalog-projection", 1, batchSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionRebuildRequest(
            "catalog-projection",
            1,
            batchSize: ProjectionRebuildRequest.MaxBatchSize + 1));
        Assert.Throws<ArgumentException>(() => new ProjectionRebuildCheckpointKey(
            "ordering",
            Guid.Empty,
            "catalog-projection",
            "tenant-a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionWriteResult(-1));
        Assert.Throws<ArgumentException>(() => ProjectionRebuildCheckpoint.Start(1, default));
    }

    [Fact]
    public void Registry_resolves_stores_by_normalized_module_and_rejects_missing_or_duplicate_stores()
    {
        RecordingCheckpointStore store = new(" Ordering ");
        using ServiceProvider provider = BuildProvider([store]);
        using IServiceScope scope = provider.CreateScope();
        IProjectionRebuildCheckpointStoreRegistry registry =
            scope.ServiceProvider.GetRequiredService<IProjectionRebuildCheckpointStoreRegistry>();

        Assert.Same(store, registry.GetRequired("ordering"));
        Assert.Throws<InvalidOperationException>(() => registry.GetRequired("catalog"));

        RecordingCheckpointStore duplicate = new("ordering");
        using ServiceProvider duplicateProvider = BuildProvider([store, duplicate]);
        using IServiceScope duplicateScope = duplicateProvider.CreateScope();

        Assert.Throws<InvalidOperationException>(() =>
            duplicateScope.ServiceProvider.GetRequiredService<IProjectionRebuildCheckpointStoreRegistry>());
    }

    [Fact]
    public void Registry_resolves_transaction_boundaries_by_normalized_module_and_rejects_duplicates()
    {
        RecordingTransactionBoundary boundary = new(" Ordering ");
        using ServiceProvider provider = BuildProvider([], boundaries: [boundary]);
        using IServiceScope scope = provider.CreateScope();
        IProjectionRebuildTransactionBoundaryRegistry registry =
            scope.ServiceProvider.GetRequiredService<IProjectionRebuildTransactionBoundaryRegistry>();

        Assert.Same(boundary, registry.GetOptional("ordering"));
        Assert.Null(registry.GetOptional("catalog"));

        RecordingTransactionBoundary duplicate = new("ordering");
        using ServiceProvider duplicateProvider = BuildProvider([], boundaries: [boundary, duplicate]);
        using IServiceScope duplicateScope = duplicateProvider.CreateScope();

        Assert.Throws<InvalidOperationException>(() =>
            duplicateScope.ServiceProvider.GetRequiredService<IProjectionRebuildTransactionBoundaryRegistry>());
    }

    [Fact]
    public async Task Ef_checkpoint_store_saves_updates_and_reads_tenant_scoped_checkpoint_state()
    {
        await using TestProjectionDbContext dbContext = CreateProjectionDbContext();
        TestProjectionCheckpointStore store = new(dbContext, "ordering", scopeAware: true);
        ProjectionRebuildCheckpointKey key = new(
            "ordering",
            RunId,
            "catalog-item-projections",
            "tenant-a");
        ProjectionRebuildCheckpoint checkpoint = ProjectionRebuildCheckpoint.Start(1, Now, "sku-001");

        await store.SaveAsync(key, checkpoint, CancellationToken.None);
        ProjectionRebuildCheckpoint? saved = await store.GetAsync(key, CancellationToken.None);

        Assert.Equal(checkpoint, saved);

        ProjectionRebuildCheckpoint completed = checkpoint
            .Advance("sku-002", 1, new ProjectionWriteResult(1), Now.AddSeconds(1))
            .Complete(Now.AddSeconds(2));
        await store.SaveAsync(key, completed, CancellationToken.None);

        ProjectionRebuildCheckpoint? updated = await store.GetAsync(key, CancellationToken.None);
        TestCheckpointState state = Assert.Single(await dbContext.Checkpoints.ToArrayAsync());
        Assert.Equal(completed, updated);
        Assert.Equal("tenant-a", state.ScopeId);
        Assert.Equal("catalog-item-projections", state.ProjectionName);
        Assert.Equal(RunId, state.RunId);
        Assert.Equal(1, await dbContext.Checkpoints.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(
            new ProjectionRebuildCheckpointKey("catalog", RunId, "catalog-item-projections", "tenant-a"),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(
            new ProjectionRebuildCheckpointKey("ordering", RunId, "catalog-item-projections", scopeId: null),
            checkpoint,
            CancellationToken.None));
    }

    [Fact]
    public async Task Ef_checkpoint_store_can_use_global_scope_when_tenant_scope_is_disabled()
    {
        await using TestProjectionDbContext dbContext = CreateProjectionDbContext();
        TestProjectionCheckpointStore store = new(dbContext, "reporting", scopeAware: false);
        ProjectionRebuildCheckpointKey key = new(
            "reporting",
            RunId,
            "global-report-projection",
            scopeId: null);
        ProjectionRebuildCheckpoint checkpoint = ProjectionRebuildCheckpoint.Start(1, Now);

        await store.SaveAsync(key, checkpoint, CancellationToken.None);

        TestCheckpointState state = Assert.Single(await dbContext.Checkpoints.ToArrayAsync());
        ProjectionRebuildCheckpoint? saved = await store.GetAsync(key, CancellationToken.None);
        Assert.Equal(ProjectionRebuildCheckpointState.GlobalScope, state.ScopeId);
        Assert.Equal(checkpoint, saved);
    }

    [Fact]
    public void Ef_checkpoint_configuration_applies_common_key_and_lengths()
    {
        using TestProjectionDbContext dbContext = CreateProjectionDbContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType =
            dbContext.Model.FindEntityType(typeof(TestCheckpointState)) ??
            throw new InvalidOperationException("Test checkpoint entity was not configured.");

        Assert.Equal(
            [
                nameof(ProjectionRebuildCheckpointState.ScopeId),
                nameof(ProjectionRebuildCheckpointState.ProjectionName),
                nameof(ProjectionRebuildCheckpointState.RunId)
            ],
            entityType.FindPrimaryKey()?.Properties.Select(property => property.Name));
        Assert.Equal(
            ScopeIds.MaxLength,
            entityType.FindProperty(nameof(ProjectionRebuildCheckpointState.ScopeId))?.GetMaxLength());
        Assert.Equal(
            ProjectionRebuildCheckpointState.ProjectionNameMaxLength,
            entityType.FindProperty(nameof(ProjectionRebuildCheckpointState.ProjectionName))?.GetMaxLength());
        Assert.Equal(
            ProjectionRebuildCheckpointState.CursorMaxLength,
            entityType.FindProperty(nameof(ProjectionRebuildCheckpointState.Cursor))?.GetMaxLength());
    }

    [Fact]
    public async Task Ef_transaction_boundary_normalizes_module_and_executes_operation()
    {
        await using TestProjectionDbContext dbContext = CreateProjectionDbContext();
        TestProjectionTransactionBoundary boundary = new(dbContext, " Ordering ");
        bool called = false;

        int result = await boundary.ExecuteAsync(
            _ =>
            {
                called = true;
                return Task.FromResult(42);
            },
            CancellationToken.None);

        Assert.True(called);
        Assert.Equal(42, result);
        Assert.Equal("ordering", boundary.ModuleName);
    }

    [Fact]
    public async Task Runner_processes_batches_persists_checkpoint_reports_progress_and_records_metrics()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        RecordingCheckpointStore store = new("ordering");
        RecordingObserver observer = new();
        using ServiceProvider provider = BuildProvider([store]);
        using IServiceScope scope = provider.CreateScope();
        ProjectionRebuildRunner<string> runner =
            scope.ServiceProvider.GetRequiredService<ProjectionRebuildRunner<string>>();
        RecordingSource source = new(
            new ProjectionReadBatch<string>(["a", "b"], "b", hasMore: true),
            new ProjectionReadBatch<string>(["c"], "c", hasMore: false));
        RecordingWriter writer = new(snapshotCount => new ProjectionWriteResult(snapshotCount));
        ProjectionRebuildExecutionContext context = CreateRebuildContext();
        ProjectionRebuildRequest request = new("catalog-item-projections", 1, batchSize: 2);

        ProjectionRebuildSummary summary = await runner.RunAsync(
            "ordering",
            request,
            source,
            writer,
            context,
            scopeAware: true,
            observer: observer,
            cancellationToken: CancellationToken.None);

        Assert.Equal("ordering", summary.ModuleName);
        Assert.Equal("catalog-item-projections", summary.ProjectionName);
        Assert.Equal("tenant-a", summary.ScopeId);
        Assert.True(summary.Checkpoint.IsCompleted);
        Assert.Equal("c", summary.Checkpoint.Cursor);
        Assert.Equal(3, summary.Checkpoint.ProcessedCount);
        Assert.Equal(3, summary.Checkpoint.WrittenCount);
        Assert.Equal([null, "b"], source.Cursors);
        Assert.Equal([["a", "b"], ["c"]], writer.WrittenBatches);
        Assert.Equal(2, store.SaveCount);
        Assert.Equal(summary.Checkpoint, Assert.Single(store.Checkpoints.Values));
        Assert.Equal(2, observer.PauseCount);
        Assert.Equal([99, 100], observer.Progress.Select(progress => progress.PercentComplete));
        Assert.Contains(observer.Progress, progress =>
            progress.Message.Contains("processed=3", StringComparison.Ordinal) &&
            progress.Message.Contains("cursor=c", StringComparison.Ordinal));
        Assert.Contains(measurements, measurement =>
            measurement.InstrumentName == "gma.projections.rebuild.rows" &&
            measurement.Value == 2 &&
            (string?)measurement.Tags[ObservabilityTagNames.Module] == "ordering" &&
            (string?)measurement.Tags[ObservabilityTagNames.Operation] == "catalog-item-projections" &&
            (string?)measurement.Tags[ObservabilityTagNames.Result] == "success" &&
            !measurement.Tags.Keys.Any(key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Runner_executes_writer_and_checkpoint_save_inside_optional_transaction_boundary()
    {
        RecordingTransactionBoundary boundary = new("ordering");
        RecordingCheckpointStore store = new("ordering", () => boundary.IsExecuting);
        using ServiceProvider provider = BuildProvider([store], boundaries: [boundary]);
        using IServiceScope scope = provider.CreateScope();
        ProjectionRebuildRunner<string> runner =
            scope.ServiceProvider.GetRequiredService<ProjectionRebuildRunner<string>>();
        RecordingSource source = new(new ProjectionReadBatch<string>(["a"], "a", hasMore: false));
        RecordingWriter writer = new(
            snapshotCount => new ProjectionWriteResult(snapshotCount),
            () => boundary.IsExecuting);

        ProjectionRebuildSummary summary = await runner.RunAsync(
            "ordering",
            new ProjectionRebuildRequest("catalog-item-projections", 1),
            source,
            writer,
            CreateRebuildContext(),
            scopeAware: true,
            observer: ProjectionRebuildRunObserver.None,
            cancellationToken: CancellationToken.None);

        Assert.True(summary.Checkpoint.IsCompleted);
        Assert.Equal(1, boundary.ExecutionCount);
        Assert.Equal(1, boundary.CommitCount);
        Assert.Equal(0, boundary.RollbackCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Runner_rolls_back_optional_transaction_boundary_when_writer_reports_failed_rows()
    {
        RecordingTransactionBoundary boundary = new("ordering");
        RecordingCheckpointStore store = new("ordering", () => boundary.IsExecuting);
        using ServiceProvider provider = BuildProvider([store], boundaries: [boundary]);
        using IServiceScope scope = provider.CreateScope();
        ProjectionRebuildRunner<string> runner =
            scope.ServiceProvider.GetRequiredService<ProjectionRebuildRunner<string>>();
        RecordingSource source = new(new ProjectionReadBatch<string>(["a"], "a", hasMore: false));
        RecordingWriter writer = new(_ => new ProjectionWriteResult(writtenCount: 0, failedCount: 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(
            "ordering",
            new ProjectionRebuildRequest("catalog-item-projections", 1),
            source,
            writer,
            CreateRebuildContext(),
            scopeAware: true,
            observer: ProjectionRebuildRunObserver.None,
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, boundary.ExecutionCount);
        Assert.Equal(0, boundary.CommitCount);
        Assert.Equal(1, boundary.RollbackCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Empty(store.Checkpoints);
    }

    [Fact]
    public async Task Runner_resumes_from_existing_checkpoint_when_payload_cursor_is_not_supplied()
    {
        ProjectionRebuildCheckpointKey key = new(
            "ordering",
            RunId,
            "catalog-item-projections",
            "tenant-a");
        RecordingCheckpointStore store = new("ordering");
        store.Seed(key, new ProjectionRebuildCheckpoint(
            "b",
            processedCount: 2,
            writtenCount: 2,
            skippedCount: 0,
            failedCount: 0,
            projectionVersion: 1,
            updatedAtUtc: Now));
        using ServiceProvider provider = BuildProvider([store]);
        using IServiceScope scope = provider.CreateScope();
        ProjectionRebuildRunner<string> runner =
            scope.ServiceProvider.GetRequiredService<ProjectionRebuildRunner<string>>();
        RecordingSource source = new(new ProjectionReadBatch<string>(["c"], "c", hasMore: false));
        RecordingWriter writer = new(snapshotCount => new ProjectionWriteResult(snapshotCount));

        ProjectionRebuildSummary summary = await runner.RunAsync(
            "ordering",
            new ProjectionRebuildRequest("catalog-item-projections", 1),
            source,
            writer,
            CreateRebuildContext(),
            scopeAware: true,
            observer: ProjectionRebuildRunObserver.None,
            cancellationToken: CancellationToken.None);

        Assert.Equal(["b"], source.Cursors);
        Assert.Equal(3, summary.Checkpoint.ProcessedCount);
        Assert.Equal(3, summary.Checkpoint.WrittenCount);
        Assert.True(summary.Checkpoint.IsCompleted);
    }

    [Fact]
    public async Task Runner_payload_cursor_override_starts_a_new_rebuild_even_when_checkpoint_exists()
    {
        ProjectionRebuildCheckpointKey key = new(
            "ordering",
            RunId,
            "catalog-item-projections",
            "tenant-a");
        RecordingCheckpointStore store = new("ordering");
        store.Seed(key, new ProjectionRebuildCheckpoint(
            "b",
            processedCount: 2,
            writtenCount: 2,
            skippedCount: 0,
            failedCount: 0,
            projectionVersion: 1,
            updatedAtUtc: Now));
        using ServiceProvider provider = BuildProvider([store]);
        using IServiceScope scope = provider.CreateScope();
        ProjectionRebuildRunner<string> runner =
            scope.ServiceProvider.GetRequiredService<ProjectionRebuildRunner<string>>();
        RecordingSource source = new(new ProjectionReadBatch<string>(["z"], "z", hasMore: false));
        RecordingWriter writer = new(snapshotCount => new ProjectionWriteResult(snapshotCount));

        ProjectionRebuildSummary summary = await runner.RunAsync(
            "ordering",
            new ProjectionRebuildRequest("catalog-item-projections", 1, cursor: " y "),
            source,
            writer,
            CreateRebuildContext(),
            scopeAware: true,
            observer: ProjectionRebuildRunObserver.None,
            cancellationToken: CancellationToken.None);

        Assert.Equal(["y"], source.Cursors);
        Assert.Equal(1, summary.Checkpoint.ProcessedCount);
        Assert.Equal(1, summary.Checkpoint.WrittenCount);
        Assert.True(summary.Checkpoint.IsCompleted);
    }

    [Fact]
    public async Task Runner_does_not_advance_checkpoint_when_writer_fails_a_batch()
    {
        RecordingCheckpointStore store = new("ordering");
        using ServiceProvider provider = BuildProvider([store]);
        using IServiceScope scope = provider.CreateScope();
        ProjectionRebuildRunner<string> runner =
            scope.ServiceProvider.GetRequiredService<ProjectionRebuildRunner<string>>();
        RecordingSource source = new(new ProjectionReadBatch<string>(["a"], "a", hasMore: false));
        RecordingWriter writer = new(_ => new ProjectionWriteResult(writtenCount: 0, failedCount: 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(
            "ordering",
            new ProjectionRebuildRequest("catalog-item-projections", 1),
            source,
            writer,
            CreateRebuildContext(),
            scopeAware: true,
            observer: ProjectionRebuildRunObserver.None,
            cancellationToken: CancellationToken.None));

        Assert.Empty(store.Checkpoints);
    }

    [Fact]
    public async Task Task_bridge_adapts_task_context_to_core_rebuild_observer()
    {
        RecordingCheckpointStore store = new("ordering");
        RecordingTaskReporter reporter = new();
        RecordingTaskControlLoop controlLoop = new();
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddOptions();
        services.Configure<ApplicationIdentityOptions>(options => options.Namespace = "gma");
        services.AddProjectionRebuildTasks();
        services.AddSingleton<ISystemClock>(new FixedClock(Now));
        services.AddSingleton<ITaskRuntimeReporter>(reporter);
        services.AddSingleton<ITaskControlLoop>(controlLoop);
        services.AddSingleton<IProjectionRebuildCheckpointStore>(store);
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        TaskProjectionRebuildRunner<string> runner =
            scope.ServiceProvider.GetRequiredService<TaskProjectionRebuildRunner<string>>();
        RecordingSource source = new(new ProjectionReadBatch<string>(["a"], "a", hasMore: false));
        RecordingWriter writer = new(snapshotCount => new ProjectionWriteResult(snapshotCount));
        TaskExecutionContext taskContext = CreateTaskContext();

        ProjectionRebuildSummary summary = await runner.RunAsync(
            "ordering",
            new ProjectionRebuildRequest("catalog-item-projections", 1),
            source,
            writer,
            taskContext,
            scopeAware: true,
            CancellationToken.None);

        Assert.True(summary.Checkpoint.IsCompleted);
        Assert.Equal("tenant-a", summary.ScopeId);
        Assert.Equal([100], reporter.Progress.Select(progress => progress.PercentComplete));
        Assert.Equal([taskContext.RunId], controlLoop.PolledRunIds);
    }

    private static ServiceProvider BuildProvider(
        IReadOnlyCollection<IProjectionRebuildCheckpointStore> stores,
        IReadOnlyCollection<IProjectionRebuildTransactionBoundary>? boundaries = null)
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddOptions();
        services.Configure<ApplicationIdentityOptions>(options => options.Namespace = "gma");
        services.AddProjectionRebuild();
        services.AddSingleton<ISystemClock>(new FixedClock(Now));

        foreach (IProjectionRebuildCheckpointStore store in stores)
        {
            services.AddSingleton(store);
        }

        foreach (IProjectionRebuildTransactionBoundary boundary in boundaries ?? [])
        {
            services.AddSingleton(boundary);
        }

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static TestProjectionDbContext CreateProjectionDbContext()
    {
        DbContextOptions<TestProjectionDbContext> options =
            new DbContextOptionsBuilder<TestProjectionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        return new TestProjectionDbContext(options);
    }

    private static ProjectionRebuildExecutionContext CreateRebuildContext() => new(RunId, "tenant-a");

    private static TaskExecutionContext CreateTaskContext() =>
        new(
            RunId,
            "ordering",
            "rebuild-catalog-item-projections",
            "projection-workers",
            "worker-a",
            "node-a",
            attempt: 1,
            scopeId: "tenant-a");

    private static MeterListener CreateListener(ICollection<MetricMeasurement> measurements)
    {
        MeterListener listener = new()
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (string.Equals(instrument.Meter.Name, "gma.projections", StringComparison.Ordinal))
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

    private static Dictionary<string, object?> ToDictionary(
        ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        tags.ToArray().ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private sealed record MetricMeasurement(
        string InstrumentName,
        double Value,
        Dictionary<string, object?> Tags);

    private sealed class RecordingSource(params ProjectionReadBatch<string>[] batches)
        : IProjectionRebuildSource<string>
    {
        private readonly Queue<ProjectionReadBatch<string>> remainingBatches = new(batches);

        public List<string?> Cursors { get; } = [];

        public Task<ProjectionReadBatch<string>> ReadAsync(
            ProjectionRebuildRequest request,
            string? cursor,
            CancellationToken cancellationToken)
        {
            this.Cursors.Add(cursor);

            return Task.FromResult(
                this.remainingBatches.Count == 0
                    ? new ProjectionReadBatch<string>([], cursor, hasMore: false)
                    : this.remainingBatches.Dequeue());
        }
    }

    private sealed class RecordingWriter(
        Func<int, ProjectionWriteResult> createResult,
        Func<bool>? canWrite = null)
        : IProjectionRebuildWriter<string>
    {
        public List<IReadOnlyList<string>> WrittenBatches { get; } = [];

        public Task<ProjectionWriteResult> WriteAsync(
            ProjectionRebuildRequest request,
            IReadOnlyCollection<string> snapshots,
            CancellationToken cancellationToken)
        {
            if (canWrite is not null && !canWrite())
            {
                throw new InvalidOperationException("Projection writer was not executed inside the expected transaction boundary.");
            }

            string[] batch = snapshots.ToArray();
            this.WrittenBatches.Add(batch);
            return Task.FromResult(createResult(batch.Length));
        }
    }

    private sealed class RecordingCheckpointStore(
        string moduleName,
        Func<bool>? canSave = null) : IProjectionRebuildCheckpointStore
    {
        public string ModuleName { get; } = moduleName;
        public Dictionary<ProjectionRebuildCheckpointKey, ProjectionRebuildCheckpoint> Checkpoints { get; } = [];
        public int SaveCount { get; private set; }

        public Task<ProjectionRebuildCheckpoint?> GetAsync(
            ProjectionRebuildCheckpointKey key,
            CancellationToken cancellationToken)
        {
            this.Checkpoints.TryGetValue(key, out ProjectionRebuildCheckpoint? checkpoint);
            return Task.FromResult(checkpoint);
        }

        public Task SaveAsync(
            ProjectionRebuildCheckpointKey key,
            ProjectionRebuildCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            if (canSave is not null && !canSave())
            {
                throw new InvalidOperationException("Projection checkpoint was not saved inside the expected transaction boundary.");
            }

            this.Checkpoints[key] = checkpoint;
            this.SaveCount++;
            return Task.CompletedTask;
        }

        public void Seed(ProjectionRebuildCheckpointKey key, ProjectionRebuildCheckpoint checkpoint) =>
            this.Checkpoints[key] = checkpoint;
    }

    private sealed class RecordingTransactionBoundary(string moduleName) : IProjectionRebuildTransactionBoundary
    {
        public string ModuleName { get; } = moduleName;
        public bool IsExecuting { get; private set; }
        public int ExecutionCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);

            this.ExecutionCount++;
            this.IsExecuting = true;

            try
            {
                TResult result = await operation(cancellationToken).ConfigureAwait(false);
                this.CommitCount++;
                return result;
            }
            catch
            {
                this.RollbackCount++;
                throw;
            }
            finally
            {
                this.IsExecuting = false;
            }
        }
    }

    private sealed class TestProjectionDbContext(DbContextOptions<TestProjectionDbContext> options) : DbContext(options)
    {
        public DbSet<TestCheckpointState> Checkpoints => this.Set<TestCheckpointState>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<TestCheckpointState>().ConfigureProjectionRebuildCheckpointState();
    }

    private sealed class TestProjectionCheckpointStore(
        TestProjectionDbContext dbContext,
        string moduleName,
        bool scopeAware)
        : EfProjectionRebuildCheckpointStore<TestProjectionDbContext, TestCheckpointState>(
            dbContext,
            moduleName,
            scopeAware,
            TestCheckpointState.CreateEmpty);

    private sealed class TestCheckpointState : ProjectionRebuildCheckpointState
    {
        private TestCheckpointState() { }

        public static TestCheckpointState CreateEmpty() => new();
    }

    private sealed class TestProjectionTransactionBoundary(
        TestProjectionDbContext dbContext,
        string moduleName)
        : EfProjectionRebuildTransactionBoundary<TestProjectionDbContext>(dbContext, moduleName);

    private sealed class RecordingObserver : IProjectionRebuildRunObserver
    {
        public List<ProjectionRebuildProgress> Progress { get; } = [];
        public int PauseCount { get; private set; }

        public Task PauseIfRequestedAsync(
            ProjectionRebuildExecutionContext context,
            TimeSpan pollInterval,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            this.PauseCount++;
            return Task.CompletedTask;
        }

        public Task ReportProgressAsync(
            ProjectionRebuildExecutionContext context,
            ProjectionRebuildProgress progress,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            this.Progress.Add(progress);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTaskReporter : ITaskRuntimeReporter
    {
        public List<TaskProgress> Progress { get; } = [];

        public Task<TaskRunMutationOutcome> ReportHeartbeatAsync(
            TaskExecutionContext context,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(TaskRunMutationOutcome.Applied);

        public Task<TaskRunMutationOutcome> ReportProgressAsync(
            TaskExecutionContext context,
            TaskProgress progress,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            this.Progress.Add(progress);
            return Task.FromResult(TaskRunMutationOutcome.Applied);
        }
    }

    private sealed class RecordingTaskControlLoop : ITaskControlLoop
    {
        public List<Guid> PolledRunIds { get; } = [];

        public Task<TaskControlPollResult> PollAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            this.PolledRunIds.Add(context.RunId);
            return Task.FromResult(new TaskControlPollResult([]));
        }

        public Task MarkHandledAsync(
            TaskExecutionContext context,
            TaskControlMessage message,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task MarkFailedAsync(
            TaskExecutionContext context,
            TaskControlMessage message,
            string error,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

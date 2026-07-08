namespace Gma.Framework.ProjectionRebuild;

using System.Diagnostics;
using Gma.Framework.Runtime.Time;

public sealed class ProjectionRebuildRunner<TSnapshot>(
    IProjectionRebuildCheckpointStoreRegistry checkpointStores,
    IProjectionRebuildTransactionBoundaryRegistry transactionBoundaries,
    ISystemClock clock,
    ProjectionRebuildMetrics metrics)
{
    public async Task<ProjectionRebuildSummary> RunAsync(
        string moduleName,
        ProjectionRebuildRequest request,
        IProjectionRebuildSource<TSnapshot> source,
        IProjectionRebuildWriter<TSnapshot> writer,
        ProjectionRebuildExecutionContext context,
        bool tenantScoped,
        IProjectionRebuildRunObserver? observer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(context);
        observer ??= ProjectionRebuildRunObserver.None;

        if (tenantScoped && string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new InvalidOperationException(
                $"Tenant-scoped projection rebuild '{moduleName}.{request.ProjectionName}' has no tenant id.");
        }

        IProjectionRebuildCheckpointStore store = checkpointStores.GetRequired(moduleName);
        IProjectionRebuildTransactionBoundary? transactionBoundary = transactionBoundaries.GetOptional(moduleName);
        ProjectionRebuildCheckpointKey key = new(
            moduleName,
            context.RunId,
            request.ProjectionName,
            tenantScoped ? context.TenantId : null);

        ProjectionRebuildCheckpoint? saved = request.Cursor is null
            ? await store.GetAsync(key, cancellationToken).ConfigureAwait(false)
            : null;

        ProjectionRebuildCheckpoint checkpoint = saved?.ProjectionVersion == request.ProjectionVersion
            ? saved
            : ProjectionRebuildCheckpoint.Start(request.ProjectionVersion, clock.UtcNow, request.Cursor);

        string? cursor = checkpoint.Cursor;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await observer
                .PauseIfRequestedAsync(context, TimeSpan.FromSeconds(1), maxMessages: 10, cancellationToken)
                .ConfigureAwait(false);

            Stopwatch stopwatch = Stopwatch.StartNew();
            ProjectionReadBatch<TSnapshot> batch = await source
                .ReadAsync(request, cursor, cancellationToken)
                .ConfigureAwait(false);

            if (batch.Snapshots.Count == 0)
            {
                ProjectionRebuildCheckpoint completed = checkpoint.Complete(clock.UtcNow);
                await store.SaveAsync(key, completed, cancellationToken).ConfigureAwait(false);
                await observer.ReportProgressAsync(
                        context,
                        CreateProgress(completed, request.DryRun, percentComplete: 100),
                        clock.UtcNow,
                        cancellationToken)
                    .ConfigureAwait(false);

                return new ProjectionRebuildSummary(moduleName, request.ProjectionName, key.TenantId, request.DryRun, completed);
            }

            ProjectionRebuildCheckpoint visibleCheckpoint;
            try
            {
                visibleCheckpoint = await this.WriteAndSaveCheckpointAsync(
                        transactionBoundary,
                        writer,
                        store,
                        key,
                        request,
                        checkpoint,
                        batch,
                        cursor,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                this.TryRecordFailure(moduleName, request);
                throw;
            }

            this.TryRecordBatch(moduleName, request, batch.Snapshots.Count, stopwatch.Elapsed);

            int percent = visibleCheckpoint.IsCompleted ? 100 : 99;
            await observer.ReportProgressAsync(
                    context,
                    CreateProgress(visibleCheckpoint, request.DryRun, percent),
                    clock.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);

            if (visibleCheckpoint.IsCompleted)
            {
                return new ProjectionRebuildSummary(
                    moduleName,
                    request.ProjectionName,
                    key.TenantId,
                    request.DryRun,
                    visibleCheckpoint);
            }

            checkpoint = visibleCheckpoint;
            cursor = visibleCheckpoint.Cursor;
        }
    }

    private Task<ProjectionRebuildCheckpoint> WriteAndSaveCheckpointAsync(
        IProjectionRebuildTransactionBoundary? transactionBoundary,
        IProjectionRebuildWriter<TSnapshot> writer,
        IProjectionRebuildCheckpointStore store,
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildRequest request,
        ProjectionRebuildCheckpoint checkpoint,
        ProjectionReadBatch<TSnapshot> batch,
        string? cursor,
        CancellationToken cancellationToken)
    {
        return transactionBoundary is null
            ? this.WriteAndSaveCoreAsync(
                writer,
                store,
                key,
                request,
                checkpoint,
                batch,
                cursor,
                cancellationToken)
            : transactionBoundary.ExecuteAsync(
                token => this.WriteAndSaveCoreAsync(
                    writer,
                    store,
                    key,
                    request,
                    checkpoint,
                    batch,
                    cursor,
                    token),
                cancellationToken);
    }

    private async Task<ProjectionRebuildCheckpoint> WriteAndSaveCoreAsync(
        IProjectionRebuildWriter<TSnapshot> writer,
        IProjectionRebuildCheckpointStore store,
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildRequest request,
        ProjectionRebuildCheckpoint checkpoint,
        ProjectionReadBatch<TSnapshot> batch,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ProjectionWriteResult writeResult = await writer
            .WriteAsync(request, batch.Snapshots, cancellationToken)
            .ConfigureAwait(false);

        if (writeResult.FailedCount > 0)
        {
            throw new InvalidOperationException(
                $"Projection rebuild writer reported {writeResult.FailedCount} failed rows for '{key.ModuleName}.{request.ProjectionName}'.");
        }

        ProjectionRebuildCheckpoint advanced = checkpoint.Advance(
            batch.NextCursor ?? cursor,
            batch.Snapshots.Count,
            writeResult,
            clock.UtcNow);
        ProjectionRebuildCheckpoint visibleCheckpoint = batch.HasMore
            ? advanced
            : advanced.Complete(clock.UtcNow);

        await store.SaveAsync(key, visibleCheckpoint, cancellationToken).ConfigureAwait(false);
        return visibleCheckpoint;
    }

    private static ProjectionRebuildProgress CreateProgress(
        ProjectionRebuildCheckpoint checkpoint,
        bool dryRun,
        int percentComplete)
    {
        string mode = dryRun ? "dry-run" : "write";
        string message =
            $"{mode}; processed={checkpoint.ProcessedCount}; written={checkpoint.WrittenCount}; skipped={checkpoint.SkippedCount}; failed={checkpoint.FailedCount}; cursor={checkpoint.Cursor ?? "start"}";

        return new ProjectionRebuildProgress(percentComplete, message);
    }

    private void TryRecordBatch(
        string moduleName,
        ProjectionRebuildRequest request,
        long processedCount,
        TimeSpan elapsed)
    {
        try
        {
            metrics.RecordBatch(moduleName, request.ProjectionName, request.DryRun, processedCount, elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }

    private void TryRecordFailure(string moduleName, ProjectionRebuildRequest request)
    {
        try
        {
            metrics.RecordFailure(moduleName, request.ProjectionName, request.DryRun);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }
}

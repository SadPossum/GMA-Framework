namespace Gma.Framework.ProjectionRebuild;

public sealed record ProjectionRebuildCheckpoint
{
    public ProjectionRebuildCheckpoint(
        string? cursor,
        long processedCount,
        long writtenCount,
        long skippedCount,
        long failedCount,
        int projectionVersion,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? completedAtUtc = null)
    {
        this.Cursor = ProjectionReadBatch<object>.NormalizeOptionalCursor(cursor);
        this.ProcessedCount = RequireNonNegative(processedCount, nameof(processedCount));
        this.WrittenCount = RequireNonNegative(writtenCount, nameof(writtenCount));
        this.SkippedCount = RequireNonNegative(skippedCount, nameof(skippedCount));
        this.FailedCount = RequireNonNegative(failedCount, nameof(failedCount));
        this.ProjectionVersion = projectionVersion > 0
            ? projectionVersion
            : throw new ArgumentOutOfRangeException(nameof(projectionVersion), projectionVersion, "Projection version must be positive.");
        this.UpdatedAtUtc = RequireTimestamp(updatedAtUtc, nameof(updatedAtUtc));
        this.CompletedAtUtc = completedAtUtc is null
            ? null
            : RequireTimestamp(completedAtUtc.Value, nameof(completedAtUtc));
    }

    public string? Cursor { get; }
    public long ProcessedCount { get; }
    public long WrittenCount { get; }
    public long SkippedCount { get; }
    public long FailedCount { get; }
    public int ProjectionVersion { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
    public DateTimeOffset? CompletedAtUtc { get; }

    public bool IsCompleted => this.CompletedAtUtc is not null;

    public static ProjectionRebuildCheckpoint Start(
        int projectionVersion,
        DateTimeOffset nowUtc,
        string? cursor = null) =>
        new(cursor, 0, 0, 0, 0, projectionVersion, nowUtc);

    public ProjectionRebuildCheckpoint Advance(
        string? nextCursor,
        long processedCount,
        ProjectionWriteResult writeResult,
        DateTimeOffset nowUtc) =>
        new(
            nextCursor,
            this.ProcessedCount + RequireNonNegative(processedCount, nameof(processedCount)),
            this.WrittenCount + writeResult.WrittenCount,
            this.SkippedCount + writeResult.SkippedCount,
            this.FailedCount + writeResult.FailedCount,
            this.ProjectionVersion,
            nowUtc);

    public ProjectionRebuildCheckpoint Complete(DateTimeOffset nowUtc) =>
        new(
            this.Cursor,
            this.ProcessedCount,
            this.WrittenCount,
            this.SkippedCount,
            this.FailedCount,
            this.ProjectionVersion,
            nowUtc,
            completedAtUtc: nowUtc);

    private static long RequireNonNegative(long value, string parameterName) =>
        value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Projection rebuild checkpoint counts cannot be negative.");

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName) =>
        value == default
            ? throw new ArgumentException($"{parameterName} must not be the default timestamp.", parameterName)
            : value;
}

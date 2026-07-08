namespace Gma.Framework.ProjectionRebuild;

public sealed record ProjectionRebuildRequest
{
    public const int DefaultBatchSize = 500;
    public const int MaxBatchSize = 5_000;

    public ProjectionRebuildRequest(
        string projectionName,
        int projectionVersion,
        int batchSize = DefaultBatchSize,
        bool dryRun = false,
        string? cursor = null)
    {
        this.ProjectionName = ProjectionRebuildNames.NormalizeProjectionName(projectionName, nameof(projectionName));
        this.ProjectionVersion = projectionVersion > 0
            ? projectionVersion
            : throw new ArgumentOutOfRangeException(nameof(projectionVersion), projectionVersion, "Projection version must be positive.");
        this.BatchSize = batchSize is > 0 and <= MaxBatchSize
            ? batchSize
            : throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                $"Projection rebuild batch size must be between 1 and {MaxBatchSize}.");
        this.DryRun = dryRun;
        this.Cursor = ProjectionReadBatch<object>.NormalizeOptionalCursor(cursor);
    }

    public string ProjectionName { get; }
    public int ProjectionVersion { get; }
    public int BatchSize { get; }
    public bool DryRun { get; }
    public string? Cursor { get; }
}

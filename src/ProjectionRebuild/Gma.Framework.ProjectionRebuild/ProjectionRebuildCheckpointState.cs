namespace Gma.Framework.ProjectionRebuild;

using Gma.Framework.Naming;

public abstract class ProjectionRebuildCheckpointState
{
    public const int ProjectionNameMaxLength = 128;
    public const int CursorMaxLength = 512;
    public const string GlobalScope = "";

    protected ProjectionRebuildCheckpointState() { }

    protected ProjectionRebuildCheckpointState(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint,
        bool scopeAware)
        => this.Initialize(key, checkpoint, scopeAware);

    public Guid RunId { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public string ProjectionName { get; private set; } = string.Empty;
    public string? Cursor { get; private set; }
    public long ProcessedCount { get; private set; }
    public long WrittenCount { get; private set; }
    public long SkippedCount { get; private set; }
    public long FailedCount { get; private set; }
    public int ProjectionVersion { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static string NormalizeScope(string? scopeId, bool scopeAware)
    {
        if (scopeAware)
        {
            return string.IsNullOrWhiteSpace(scopeId)
                ? throw new InvalidOperationException("Scope-aware projection rebuild checkpoints require a scope id.")
                : ScopeIds.Normalize(scopeId);
        }

        return string.IsNullOrWhiteSpace(scopeId)
            ? GlobalScope
            : ScopeIds.Normalize(scopeId);
    }

    public void Initialize(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint,
        bool scopeAware)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(checkpoint);

        if (this.RunId != Guid.Empty)
        {
            throw new InvalidOperationException("Projection rebuild checkpoint state is already initialized.");
        }

        this.RunId = key.RunId;
        this.ScopeId = NormalizeScope(key.ScopeId, scopeAware);
        this.ProjectionName = NormalizeProjectionName(key.ProjectionName);
        this.Apply(checkpoint);
    }

    public void Update(ProjectionRebuildCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        this.Apply(checkpoint);
    }

    public ProjectionRebuildCheckpoint ToCheckpoint() =>
        new(
            this.Cursor,
            this.ProcessedCount,
            this.WrittenCount,
            this.SkippedCount,
            this.FailedCount,
            this.ProjectionVersion,
            this.UpdatedAtUtc,
            this.CompletedAtUtc);

    private static string NormalizeProjectionName(string projectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);

        string normalized = projectionName.Trim();
        if (normalized.Length > ProjectionNameMaxLength)
        {
            throw new ArgumentException(
                $"Projection name must be {ProjectionNameMaxLength} characters or fewer.",
                nameof(projectionName));
        }

        return normalized;
    }

    private void Apply(ProjectionRebuildCheckpoint checkpoint)
    {
        this.Cursor = NormalizeCursor(checkpoint.Cursor);
        this.ProcessedCount = checkpoint.ProcessedCount;
        this.WrittenCount = checkpoint.WrittenCount;
        this.SkippedCount = checkpoint.SkippedCount;
        this.FailedCount = checkpoint.FailedCount;
        this.ProjectionVersion = checkpoint.ProjectionVersion;
        this.UpdatedAtUtc = checkpoint.UpdatedAtUtc;
        this.CompletedAtUtc = checkpoint.CompletedAtUtc;
    }

    private static string? NormalizeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        string normalized = cursor.Trim();
        if (normalized.Length > CursorMaxLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Projection rebuild cursor must be {CursorMaxLength} characters or fewer and cannot contain control characters.",
                nameof(cursor));
        }

        return normalized;
    }
}

namespace Gma.Framework.ProjectionRebuild;

public sealed record ProjectionWriteResult
{
    public ProjectionWriteResult(long writtenCount, long skippedCount = 0, long failedCount = 0)
    {
        this.WrittenCount = RequireNonNegative(writtenCount, nameof(writtenCount));
        this.SkippedCount = RequireNonNegative(skippedCount, nameof(skippedCount));
        this.FailedCount = RequireNonNegative(failedCount, nameof(failedCount));
    }

    public long WrittenCount { get; }
    public long SkippedCount { get; }
    public long FailedCount { get; }

    private static long RequireNonNegative(long value, string parameterName) =>
        value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Projection write counts cannot be negative.");
}

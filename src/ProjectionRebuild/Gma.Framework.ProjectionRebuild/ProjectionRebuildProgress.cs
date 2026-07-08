namespace Gma.Framework.ProjectionRebuild;

public sealed record ProjectionRebuildProgress
{
    public ProjectionRebuildProgress(int percentComplete, string message)
    {
        this.PercentComplete = percentComplete is >= 0 and <= 100
            ? percentComplete
            : throw new ArgumentOutOfRangeException(
                nameof(percentComplete),
                percentComplete,
                "Projection rebuild progress must be between 0 and 100.");
        this.Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Projection rebuild progress message is required.", nameof(message))
            : message;
    }

    public int PercentComplete { get; }
    public string Message { get; }
}

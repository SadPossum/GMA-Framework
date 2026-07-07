namespace Gma.Framework.ProjectionRebuild;

public sealed record ProjectionReadBatch<TSnapshot>
{
    public ProjectionReadBatch(
        IReadOnlyCollection<TSnapshot> snapshots,
        string? nextCursor,
        bool hasMore)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (hasMore && string.IsNullOrWhiteSpace(nextCursor))
        {
            throw new ArgumentException("A projection read batch with more data must provide the next cursor.", nameof(nextCursor));
        }

        this.Snapshots = snapshots.ToArray();
        this.NextCursor = NormalizeOptionalCursor(nextCursor);
        this.HasMore = hasMore;
    }

    public IReadOnlyCollection<TSnapshot> Snapshots { get; }
    public string? NextCursor { get; }
    public bool HasMore { get; }

    internal static string? NormalizeOptionalCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        string normalized = cursor.Trim();
        return normalized.Any(char.IsControl)
            ? throw new ArgumentException("Projection cursor cannot contain control characters.", nameof(cursor))
            : normalized;
    }
}

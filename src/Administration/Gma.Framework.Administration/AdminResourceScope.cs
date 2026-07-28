namespace Gma.Framework.Administration;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

public sealed class AdminResourceScope
{
    // Leaves room for a maximum tenant segment in the 1,024-character AccessScope contract.
    public const int MaxLength = 888;

    private readonly ReadOnlyCollection<AdminResourceScopeSegment> segments;

    private AdminResourceScope(IReadOnlyList<AdminResourceScopeSegment> segments)
    {
        this.segments = new ReadOnlyCollection<AdminResourceScopeSegment>(segments.ToArray());
        this.Value = string.Join("/", this.segments.Select(segment => segment.ToString()));
    }

    public string Value { get; }
    public IReadOnlyList<AdminResourceScopeSegment> Segments => this.segments;

    public static AdminResourceScope Create(params AdminResourceScopeSegment[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Length == 0 || segments.Any(segment => segment is null))
        {
            throw new ArgumentException(
                "An admin resource scope requires at least one non-null segment.",
                nameof(segments));
        }

        int serializedLength = segments.Sum(segment => segment.Name.Length + segment.Value.Length + 1) +
            segments.Length - 1;
        if (serializedLength > MaxLength)
        {
            throw new ArgumentException(
                $"An admin resource scope must be {MaxLength} characters or fewer.",
                nameof(segments));
        }

        return new AdminResourceScope(segments);
    }

    public static AdminResourceScope Create(IEnumerable<AdminResourceScopeSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Create(segments.ToArray());
    }

    public static bool TryParse(
        string? value,
        [NotNullWhen(true)] out AdminResourceScope? resourceScope)
    {
        resourceScope = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        if (normalized.Length > MaxLength)
        {
            return false;
        }

        string[] parts = normalized.Split('/');
        List<AdminResourceScopeSegment> parsedSegments = [];
        foreach (string part in parts)
        {
            string[] segmentParts = part.Split(':');
            if (segmentParts.Length != 2 ||
                !AdminResourceScopeSegment.TryCreate(
                    segmentParts[0],
                    segmentParts[1],
                    out AdminResourceScopeSegment? segment))
            {
                return false;
            }

            parsedSegments.Add(segment);
        }

        resourceScope = new AdminResourceScope(parsedSegments);
        return true;
    }

    public static AdminResourceScope Parse(string value)
    {
        if (TryParse(value, out AdminResourceScope? resourceScope))
        {
            return resourceScope;
        }

        throw new ArgumentException(
            "An admin resource scope must contain slash-separated name:value segments.",
            nameof(value));
    }

    public override string ToString() => this.Value;
}

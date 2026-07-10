namespace Gma.Framework.AccessControl;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

public sealed class AccessScope : IEquatable<AccessScope>
{
    public const int MaxLength = 1024;
    public const string GlobalValue = "global";
    public static readonly AccessScope Global = new([]);

    private readonly ReadOnlyCollection<AccessScopeSegment> segments;

    private AccessScope(IReadOnlyList<AccessScopeSegment> segments)
    {
        this.segments = new ReadOnlyCollection<AccessScopeSegment>(segments.ToArray());
        this.Value = this.segments.Count == 0
            ? GlobalValue
            : string.Join("/", this.segments.Select(segment => segment.ToString()));

        if (this.Value.Length > MaxLength)
        {
            throw new ArgumentException($"Access scope must be {MaxLength} characters or fewer.", nameof(segments));
        }
    }

    public string Value { get; }
    public IReadOnlyList<AccessScopeSegment> Segments => this.segments;
    public bool IsGlobal => this.segments.Count == 0;

    public static AccessScope Create(params AccessScopeSegment[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Any(segment => segment is null))
        {
            throw new ArgumentException("Access scope segments cannot contain null values.", nameof(segments));
        }

        return segments.Length == 0 ? Global : new AccessScope(segments);
    }

    public static AccessScope Create(IEnumerable<AccessScopeSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Create(segments.ToArray());
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out AccessScope? scope)
    {
        scope = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        if (normalized.Length > MaxLength)
        {
            return false;
        }

        if (string.Equals(normalized, GlobalValue, StringComparison.OrdinalIgnoreCase))
        {
            scope = Global;
            return true;
        }

        string[] parts = normalized.Split('/');
        if (parts.Length == 0)
        {
            return false;
        }

        List<AccessScopeSegment> parsedSegments = [];
        foreach (string part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return false;
            }

            string[] segmentParts = part.Split(':');
            if (segmentParts.Length != 2 ||
                !AccessScopeSegment.TryCreate(segmentParts[0], segmentParts[1], out AccessScopeSegment? segment))
            {
                return false;
            }

            parsedSegments.Add(segment);
        }

        scope = new AccessScope(parsedSegments);
        return true;
    }

    public static AccessScope Parse(string value)
    {
        if (TryParse(value, out AccessScope? scope))
        {
            return scope;
        }

        throw new ArgumentException("Access scope must be 'global' or slash-separated name:value segments.", nameof(value));
    }

    public override string ToString() => this.Value;

    public bool Equals(AccessScope? other) =>
        other is not null && string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AccessScope other && this.Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(this.Value);
}

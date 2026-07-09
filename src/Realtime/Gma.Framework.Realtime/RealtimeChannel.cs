namespace Gma.Framework.Realtime;

using Gma.Framework.Naming;

public sealed class RealtimeChannel : IEquatable<RealtimeChannel>
{
    private const char Separator = '\u001f';

    private RealtimeChannel(string name, IReadOnlyList<string> segments)
    {
        this.Name = name;
        this.Segments = segments;
        this.Key = string.Join(Separator, [name, .. segments]);
    }

    public string Name { get; }
    public IReadOnlyList<string> Segments { get; }
    public string Key { get; }

    public static RealtimeChannel Create(string name, params string[] segments)
    {
        string normalizedName = SharedNameSegments.NormalizeKebabSegment(
            name,
            "realtime channel name",
            nameof(name));
        if (segments.Length == 0)
        {
            throw new ArgumentException("Realtime channels must include at least one routing segment.", nameof(segments));
        }

        string[] normalizedSegments = segments
            .Select((segment, index) => NormalizeSegment(segment, index, nameof(segments)))
            .ToArray();

        return new RealtimeChannel(normalizedName, Array.AsReadOnly(normalizedSegments));
    }

    public bool Equals(RealtimeChannel? other) =>
        other is not null &&
        string.Equals(this.Key, other.Key, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is RealtimeChannel other && this.Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Key);

    public override string ToString() => this.Name;

    private static string NormalizeSegment(string? segment, int index, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException(
                $"Realtime channel segment {index} is required.",
                parameterName);
        }

        string normalized = segment.Trim();
        if (normalized.Length > 256 ||
            normalized.Any(character => char.IsControl(character)))
        {
            throw new ArgumentException(
                $"Realtime channel segment {index} must be 256 characters or fewer and cannot contain control characters.",
                parameterName);
        }

        return normalized;
    }
}

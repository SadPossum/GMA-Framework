namespace Gma.Framework.Administration;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Naming;

public sealed record AdminResourceScopeSegment
{
    public const int NameMaxLength = 64;
    public const int ValueMaxLength = 256;

    private AdminResourceScopeSegment(string name, string value)
    {
        this.Name = name;
        this.Value = value;
    }

    public string Name { get; }
    public string Value { get; }

    public static AdminResourceScopeSegment Create(string name, string value)
    {
        if (TryCreate(name, value, out AdminResourceScopeSegment? segment))
        {
            return segment;
        }

        throw new ArgumentException(
            "An admin resource-scope segment requires a lowercase kebab-case name and a bounded value without whitespace, control characters, ':' or '/'.",
            nameof(name));
    }

    public static bool TryCreate(
        string? name,
        string? value,
        [NotNullWhen(true)] out AdminResourceScopeSegment? segment)
    {
        segment = null;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalizedName = name.Trim().ToLowerInvariant();
        if (normalizedName.Length > NameMaxLength ||
            !SharedNameSegments.IsKebabSegment(normalizedName))
        {
            return false;
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length > ValueMaxLength ||
            normalizedValue.Any(character =>
                char.IsWhiteSpace(character) ||
                char.IsControl(character) ||
                character is ':' or '/'))
        {
            return false;
        }

        segment = new AdminResourceScopeSegment(normalizedName, normalizedValue);
        return true;
    }

    public override string ToString() => $"{this.Name}:{this.Value}";
}

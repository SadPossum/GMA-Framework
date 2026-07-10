namespace Gma.Framework.AccessControl;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Naming;

public sealed record AccessScopeSegment
{
    public const int NameMaxLength = 64;
    public const int ValueMaxLength = 256;

    private AccessScopeSegment(string name, string value)
    {
        this.Name = name;
        this.Value = value;
    }

    public string Name { get; }
    public string Value { get; }

    public static AccessScopeSegment Create(string name, string value)
    {
        if (TryCreate(name, value, out AccessScopeSegment? segment))
        {
            return segment;
        }

        throw new ArgumentException(
            "Access scope segment requires a lowercase kebab-case name and a bounded value without whitespace, control characters, ':' or '/'.",
            nameof(name));
    }

    public static bool TryCreate(
        string? name,
        string? value,
        [NotNullWhen(true)] out AccessScopeSegment? segment)
    {
        segment = null;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalizedName = name.Trim().ToLowerInvariant();
        if (normalizedName.Length > NameMaxLength || !SharedNameSegments.IsKebabSegment(normalizedName))
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

        segment = new AccessScopeSegment(normalizedName, normalizedValue);
        return true;
    }

    public override string ToString() => $"{this.Name}:{this.Value}";
}

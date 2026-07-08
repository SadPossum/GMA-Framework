namespace Gma.Framework.Naming;

public static class SharedNameSegments
{
    public static string NormalizeKebabSegment(string value, string description, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (!IsKebabSegment(normalized))
        {
            throw new ArgumentException(
                $"{parameterName} must be a lowercase kebab-case {description}.",
                parameterName);
        }

        return normalized;
    }

    public static bool IsKebabSegment(string value)
    {
        if (value.Length == 0 ||
            value[0] == '-' ||
            value[^1] == '-' ||
            value.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character == '-');
    }
}

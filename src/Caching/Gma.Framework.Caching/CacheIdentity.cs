namespace Gma.Framework.Caching;

using Gma.Framework.Naming;

internal static class CacheIdentity
{
    public const int MaxNameLength = 128;
    public const int MaxSegments = 16;
    public const int SegmentMaxLength = 256;

    public static string ValidateModuleName(string value, string parameterName) =>
        SharedModuleNames.Normalize(value, parameterName);

    public static string ValidateName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim();
        if (normalized.Length > MaxNameLength || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException(
                $"Cache names must be at most {MaxNameLength} characters and contain only ASCII letters, digits, '.', '-' or '_'.",
                parameterName);
        }

        return normalized.ToLowerInvariant();
    }

    public static IReadOnlyList<string> ValidateSegments(IEnumerable<string>? segments)
    {
        string[] values = segments?.ToArray() ?? [];

        if (values.Length > MaxSegments)
        {
            throw new ArgumentException($"Cache identities can contain at most {MaxSegments} segments.", nameof(segments));
        }

        string[] normalizedValues = new string[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            string value = values[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(segments));

            string normalized = value.Trim();
            if (normalized.Length > SegmentMaxLength ||
                normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            {
                throw new ArgumentException(
                    $"Cache identity segments must be {SegmentMaxLength} characters or fewer and cannot contain whitespace or control characters.",
                    nameof(segments));
            }

            normalizedValues[index] = normalized;
        }

        return Array.AsReadOnly(normalizedValues);
    }
}

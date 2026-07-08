namespace Gma.Framework.Naming;

public static class ApplicationNamespaces
{
    public const string Default = "gma";
    public const int MaxLength = 32;

    public static string Normalize(string value, string parameterName = "applicationNamespace")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (!IsValid(normalized))
        {
            throw new ArgumentException(
                $"{parameterName} must be a lowercase kebab-case application namespace.",
                parameterName);
        }

        return normalized;
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized.Length <= MaxLength && SharedNameSegments.IsKebabSegment(normalized);
    }

    public static string CreateStreamName(string applicationNamespace)
    {
        string normalized = Normalize(applicationNamespace);
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{normalized.Replace('-', '_').ToUpperInvariant()}_EVENTS");
    }

    public static string CreateWildcardSubject(string applicationNamespace) =>
        $"{Normalize(applicationNamespace)}.>";
}

namespace Gma.Framework.Observability;

public sealed record SecuritySignalDefinition
{
    public const int CodeMaxLength = 128;

    public SecuritySignalDefinition(
        string code,
        SecuritySignalCategory category,
        SecuritySignalSeverity severity)
    {
        this.Code = NormalizeCode(code);
        this.Category = RequireKnownCategory(category);
        this.Severity = RequireKnownSeverity(severity);
    }

    public string Code { get; }
    public SecuritySignalCategory Category { get; }
    public SecuritySignalSeverity Severity { get; }

    private static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        string normalized = code.Trim();
        string[] segments = normalized.Split('.');
        if (normalized.Length > CodeMaxLength ||
            normalized.Any(char.IsAsciiLetterUpper) ||
            segments.Length < 2 ||
            segments.Any(segment =>
                segment.Length == 0 ||
                segment.StartsWith('-') ||
                segment.EndsWith('-') ||
                segment.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) &&
                    character != '-')))
        {
            throw new ArgumentException(
                $"Security signal code must be a lowercase dotted identifier, " +
                $"{CodeMaxLength} characters or fewer, with ASCII letters, digits, or '-'.",
                nameof(code));
        }

        return normalized;
    }

    private static SecuritySignalCategory RequireKnownCategory(SecuritySignalCategory category) =>
        category is not SecuritySignalCategory.Unknown && Enum.IsDefined(category)
            ? category
            : throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Security signal category is invalid.");

    private static SecuritySignalSeverity RequireKnownSeverity(SecuritySignalSeverity severity) =>
        severity is not SecuritySignalSeverity.Unknown && Enum.IsDefined(severity)
            ? severity
            : throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "Security signal severity is invalid.");
}

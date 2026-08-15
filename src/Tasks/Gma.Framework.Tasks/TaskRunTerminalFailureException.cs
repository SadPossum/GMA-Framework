namespace Gma.Framework.Tasks;

public sealed class TaskRunTerminalFailureException : Exception
{
    public const int FailureCodeMaxLength = 200;

    public TaskRunTerminalFailureException(string failureCode)
        : base(Normalize(failureCode))
        => this.FailureCode = Normalize(failureCode);

    public TaskRunTerminalFailureException(
        string failureCode,
        Exception innerException)
        : base(Normalize(failureCode), innerException)
        => this.FailureCode = Normalize(failureCode);

    public string FailureCode { get; }

    private static string Normalize(string failureCode)
    {
        string normalized = failureCode?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (normalized.Length is 0 or > FailureCodeMaxLength ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '.' and not '-' and not '_' and not ':'))
        {
            throw new ArgumentException(
                "A bounded machine-readable task failure code is required.",
                nameof(failureCode));
        }

        return normalized;
    }
}

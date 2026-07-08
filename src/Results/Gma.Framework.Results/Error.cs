namespace Gma.Framework.Results;

using System.Diagnostics.CodeAnalysis;

public sealed record Error
{
    public const int CodeMaxLength = 256;
    public const int MessageMaxLength = 1024;

    public static readonly Error None = new(string.Empty, string.Empty, allowEmpty: true);
    public static readonly Error NullValue = new("Error.NullValue", "The specified value is null.");

    public Error(string code, string message)
        : this(code, message, allowEmpty: false)
    {
    }

    private Error(string code, string message, bool allowEmpty)
    {
        this.Code = allowEmpty
            ? code
            : NormalizeCode(code);
        this.Message = allowEmpty
            ? message
            : NormalizeMessage(message);
    }

    public string Code { get; }
    public string Message { get; }

    public static string NormalizeCode(string code)
    {
        if (TryNormalizeCode(code, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"Error code must be a dotted identifier, {CodeMaxLength} characters or fewer, and contain only ASCII letters, digits, or '.'.",
            nameof(code));
    }

    public static bool TryNormalizeCode(
        string? code,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string candidate = code.Trim();
        if (candidate.Length > CodeMaxLength ||
            !candidate.Contains('.', StringComparison.Ordinal) ||
            candidate.StartsWith('.') ||
            candidate.EndsWith('.') ||
            candidate.Contains("..", StringComparison.Ordinal) ||
            candidate.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '.'))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    private static string NormalizeMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string normalized = message.Trim();
        if (normalized.Length > MessageMaxLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Error message must be {MessageMaxLength} characters or fewer and cannot contain control characters.",
                nameof(message));
        }

        return normalized;
    }
}

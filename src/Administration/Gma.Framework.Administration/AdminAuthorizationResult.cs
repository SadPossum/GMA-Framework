namespace Gma.Framework.Administration;

public sealed record AdminAuthorizationResult
{
    public const int FailureReasonMaxLength = 512;

    private AdminAuthorizationResult(bool isAuthorized, string? failureReason)
    {
        this.IsAuthorized = isAuthorized;
        this.FailureReason = failureReason;
    }

    public bool IsAuthorized { get; }
    public string? FailureReason { get; }

    public static AdminAuthorizationResult Allowed() => new(true, null);
    public static AdminAuthorizationResult Denied(string reason) => new(false, NormalizeFailureReason(reason));

    private static string NormalizeFailureReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        string normalized = reason.Trim();
        if (normalized.Length > FailureReasonMaxLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Admin authorization failure reason must be {FailureReasonMaxLength} characters or fewer and cannot contain control characters.",
                nameof(reason));
        }

        return normalized;
    }
}

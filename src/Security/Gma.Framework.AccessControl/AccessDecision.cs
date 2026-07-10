namespace Gma.Framework.AccessControl;

public sealed record AccessDecision
{
    public const int ReasonCodeMaxLength = 128;
    public const int MessageMaxLength = 512;

    private AccessDecision(AccessDecisionOutcome outcome, string reasonCode, string? message)
    {
        if (outcome == AccessDecisionOutcome.Unknown || !Enum.IsDefined(outcome))
        {
            throw new ArgumentException("Access decision outcome must be a defined non-unknown value.", nameof(outcome));
        }

        this.Outcome = outcome;
        this.ReasonCode = AccessText.NormalizeIdentifier(
            reasonCode,
            ReasonCodeMaxLength,
            "Access decision reason code",
            nameof(reasonCode));
        this.Message = NormalizeMessage(message);
    }

    public AccessDecisionOutcome Outcome { get; }
    public string ReasonCode { get; }
    public string? Message { get; }
    public bool IsAllowed => this.Outcome == AccessDecisionOutcome.Allowed;
    public bool IsDenied => this.Outcome == AccessDecisionOutcome.Denied;
    public bool IsAbstain => this.Outcome == AccessDecisionOutcome.Abstain;

    public static AccessDecision Allowed(string reasonCode = AccessDecisionReasonCodes.Allowed, string? message = null) =>
        new(AccessDecisionOutcome.Allowed, reasonCode, message);

    public static AccessDecision Denied(string reasonCode, string? message = null) =>
        new(AccessDecisionOutcome.Denied, reasonCode, message);

    public static AccessDecision Abstain(string reasonCode = AccessDecisionReasonCodes.ProviderAbstained, string? message = null) =>
        new(AccessDecisionOutcome.Abstain, reasonCode, message);

    private static string? NormalizeMessage(string? message)
    {
        if (message is null)
        {
            return null;
        }

        string normalized = message.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        if (normalized.Length > MessageMaxLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Access decision message must be {MessageMaxLength} characters or fewer and cannot contain control characters.",
                nameof(message));
        }

        return normalized;
    }
}

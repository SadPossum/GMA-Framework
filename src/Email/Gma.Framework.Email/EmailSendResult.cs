namespace Gma.Framework.Email;

public enum EmailSendOutcome
{
    Unknown = 0,
    Delivered = 1,
    Retry = 2,
    Rejected = 3
}

public sealed record EmailSendResult(
    EmailSendOutcome Outcome,
    string? ProviderMessageId = null,
    string? Code = null,
    DateTimeOffset? RetryAtUtc = null)
{
    public static EmailSendResult Delivered(string? providerMessageId = null) =>
        new(EmailSendOutcome.Delivered, providerMessageId);

    public static EmailSendResult Retry(string code, DateTimeOffset? retryAtUtc = null) =>
        new(EmailSendOutcome.Retry, Code: code, RetryAtUtc: retryAtUtc);

    public static EmailSendResult Rejected(string code) =>
        new(EmailSendOutcome.Rejected, Code: code);
}

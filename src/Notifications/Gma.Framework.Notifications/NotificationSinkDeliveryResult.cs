namespace Gma.Framework.Notifications;

using Gma.Framework.Naming;

public enum NotificationSinkDeliveryOutcome
{
    Unknown = 0,
    Delivered = 1,
    Retry = 2,
    Rejected = 3,
    Skipped = 4
}

public sealed record NotificationSinkDeliveryResult
{
    public const int ProviderMessageIdMaxLength = 512;
    public const int CodeMaxLength = 128;

    private NotificationSinkDeliveryResult(
        NotificationSinkDeliveryOutcome outcome,
        string? providerMessageId,
        string? code,
        DateTimeOffset? retryAtUtc)
    {
        this.Outcome = outcome;
        this.ProviderMessageId = NormalizeOptional(providerMessageId, ProviderMessageIdMaxLength, nameof(providerMessageId));
        this.Code = string.IsNullOrWhiteSpace(code)
            ? null
            : NormalizeCode(code);
        this.RetryAtUtc = retryAtUtc;
    }

    public NotificationSinkDeliveryOutcome Outcome { get; }
    public string? ProviderMessageId { get; }
    public string? Code { get; }
    public DateTimeOffset? RetryAtUtc { get; }

    public static NotificationSinkDeliveryResult Delivered(string? providerMessageId = null) =>
        new(NotificationSinkDeliveryOutcome.Delivered, providerMessageId, code: null, retryAtUtc: null);

    public static NotificationSinkDeliveryResult Retry(string code, DateTimeOffset? retryAtUtc = null) =>
        new(NotificationSinkDeliveryOutcome.Retry, providerMessageId: null, code, retryAtUtc);

    public static NotificationSinkDeliveryResult Rejected(string code) =>
        new(NotificationSinkDeliveryOutcome.Rejected, providerMessageId: null, code, retryAtUtc: null);

    public static NotificationSinkDeliveryResult Skipped(string code) =>
        new(NotificationSinkDeliveryOutcome.Skipped, providerMessageId: null, code, retryAtUtc: null);

    private static string NormalizeCode(string code)
    {
        string normalized = SharedNameSegments.NormalizeKebabSegment(code, "delivery result code", nameof(code));
        return normalized.Length <= CodeMaxLength
            ? normalized
            : throw new ArgumentException(
                $"Delivery result codes must be {CodeMaxLength} characters or fewer.",
                nameof(code));
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{parameterName} must be {maxLength} characters or fewer and cannot contain control characters.",
                parameterName);
        }

        return normalized;
    }
}

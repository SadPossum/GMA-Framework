namespace Gma.Framework.Email;

using System.Net.Mail;

public sealed class EmailSendRequest
{
    public const int SubjectMaxLength = 512;
    public const int BodyMaxLength = 2_000_000;
    public const int IdempotencyKeyMaxLength = 256;

    public EmailSendRequest(
        string recipientAddress,
        string subject,
        string? textBody,
        string? htmlBody,
        string idempotencyKey,
        string? senderAddress = null,
        string? senderName = null)
    {
        this.RecipientAddress = NormalizeAddress(recipientAddress, nameof(recipientAddress)) ??
                                throw new ArgumentException("Recipient email address is required.", nameof(recipientAddress));
        this.SenderAddress = NormalizeAddress(senderAddress, nameof(senderAddress));
        this.SenderName = NormalizeOptional(senderName, 256, nameof(senderName));
        this.Subject = NormalizeRequired(subject, SubjectMaxLength, nameof(subject));
        this.TextBody = NormalizeOptionalBody(textBody, nameof(textBody));
        this.HtmlBody = NormalizeOptionalBody(htmlBody, nameof(htmlBody));
        this.IdempotencyKey = NormalizeRequired(idempotencyKey, IdempotencyKeyMaxLength, nameof(idempotencyKey));

        if (this.TextBody is null && this.HtmlBody is null)
        {
            throw new ArgumentException("At least one email body representation is required.", nameof(textBody));
        }
    }

    public string RecipientAddress { get; }
    public string Subject { get; }
    public string? TextBody { get; }
    public string? HtmlBody { get; }
    public string IdempotencyKey { get; }
    public string? SenderAddress { get; }
    public string? SenderName { get; }

    public static bool IsValidAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        MailAddress.TryCreate(value.Trim(), out MailAddress? address) &&
        string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeAddress(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (!IsValidAddress(normalized))
        {
            throw new ArgumentException("Email address is invalid.", parameterName);
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        string? normalized = NormalizeOptional(value, maxLength, parameterName);
        return normalized ?? throw new ArgumentException("Value is required.", parameterName);
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
            throw new ArgumentException($"Value must be {maxLength} characters or fewer and cannot contain control characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptionalBody(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > BodyMaxLength)
        {
            throw new ArgumentException($"Email body must be {BodyMaxLength} characters or fewer.", parameterName);
        }

        return value;
    }
}

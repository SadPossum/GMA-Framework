namespace Gma.Framework.Notifications;

public sealed record NotificationPublishOptions
{
    public const int TitleMaxLength = 256;
    public const int BodyMaxLength = 4096;

    public NotificationPublishOptions(
        string title,
        string? body = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        Guid? id = null,
        DateTimeOffset? occurredAtUtc = null,
        IReadOnlyCollection<string>? tags = null,
        NotificationDeliveryPolicy deliveryPolicy = NotificationDeliveryPolicy.RespectPreferences)
    {
        this.Title = NormalizeText(title, TitleMaxLength, nameof(title), required: true)!;
        this.Body = NormalizeText(body, BodyMaxLength, nameof(body), required: false);
        this.Severity = NotificationSeverities.Normalize(severity, nameof(severity));
        this.Id = id;
        this.OccurredAtUtc = occurredAtUtc;
        this.Tags = NotificationTags.Copy(tags);
        this.DeliveryPolicy = NotificationDeliveryPolicies.Normalize(deliveryPolicy);
    }

    public string Title { get; }
    public string? Body { get; }
    public NotificationSeverity Severity { get; }
    public Guid? Id { get; }
    public DateTimeOffset? OccurredAtUtc { get; }
    public IReadOnlyList<string> Tags { get; }
    public NotificationDeliveryPolicy DeliveryPolicy { get; }

    private static string? NormalizeText(string? value, int maxLength, string parameterName, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new ArgumentException("Notification text is required.", parameterName);
            }

            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maxLength ||
            normalized.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
        {
            throw new ArgumentException(
                $"Notification text must be {maxLength} characters or fewer and cannot contain control characters.",
                parameterName);
        }

        return normalized;
    }
}

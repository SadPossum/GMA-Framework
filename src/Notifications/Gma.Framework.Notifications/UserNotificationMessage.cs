namespace Gma.Framework.Notifications;

using System.Text.Json;
using Gma.Framework.Naming;

public sealed record UserNotificationMessage
{
    public UserNotificationMessage(
        Guid id,
        string module,
        string name,
        int version,
        string scopeId,
        string userId,
        string title,
        string? body,
        NotificationSeverity severity,
        DateTimeOffset occurredAtUtc,
        JsonElement payload,
        IReadOnlyList<string>? tags = null,
        NotificationDeliveryPolicy deliveryPolicy = NotificationDeliveryPolicy.RespectPreferences)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Notification id must not be empty.", nameof(id));
        }

        this.Id = id;
        this.Module = SharedNameSegments.NormalizeKebabSegment(module, "module name", nameof(module));
        this.Name = NotificationNames.NormalizeName(name, nameof(name));
        this.Version = NotificationVersions.Normalize(version, nameof(version));
        this.ScopeId = ScopeIds.Normalize(scopeId);
        this.UserId = NotificationUserIds.Normalize(userId);
        this.Title = new NotificationPublishOptions(title).Title;
        this.Body = string.IsNullOrWhiteSpace(body)
            ? null
            : new NotificationPublishOptions(title, body).Body;
        this.Severity = NotificationSeverities.Normalize(severity, nameof(severity));
        this.OccurredAtUtc = occurredAtUtc;
        this.Payload = payload.Clone();
        this.Tags = NotificationTags.Copy(tags);
        this.DeliveryPolicy = NotificationDeliveryPolicies.Normalize(deliveryPolicy);
    }

    public Guid Id { get; }
    public string Module { get; }
    public string Name { get; }
    public int Version { get; }
    public string ScopeId { get; }
    public string UserId { get; }
    public string Title { get; }
    public string? Body { get; }
    public NotificationSeverity Severity { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public JsonElement Payload { get; }
    public IReadOnlyList<string> Tags { get; }
    public NotificationDeliveryPolicy DeliveryPolicy { get; }
}

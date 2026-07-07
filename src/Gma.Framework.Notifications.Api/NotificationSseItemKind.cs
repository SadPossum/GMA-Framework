namespace Gma.Framework.Notifications.Api;

using System.Text.Json.Serialization;

[JsonConverter(typeof(NotificationSseItemKindJsonConverter))]
public enum NotificationSseItemKind
{
    Unknown = 0,
    Notification = 1,
    Heartbeat = 2
}

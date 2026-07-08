namespace Gma.Framework.Notifications;

using System.Text.Json.Serialization;

[JsonConverter(typeof(NotificationSeverityJsonConverter))]
public enum NotificationSeverity
{
    Unknown = 0,
    Info = 1,
    Success = 2,
    Warning = 3,
    Error = 4
}

namespace Gma.Framework.Notifications.Api;

public sealed class NotificationSseOptions
{
    public const string SectionName = "Notifications:Sse";
    public const string DefaultStreamPath = "/api/notifications/stream";
    public const string DefaultNotificationEventType = "notification";
    public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(15);

    public bool Enabled { get; set; } = true;
    public string StreamPath { get; set; } = DefaultStreamPath;
    public string NotificationEventType { get; set; } = DefaultNotificationEventType;
    public TimeSpan HeartbeatInterval { get; set; } = DefaultHeartbeatInterval;
}

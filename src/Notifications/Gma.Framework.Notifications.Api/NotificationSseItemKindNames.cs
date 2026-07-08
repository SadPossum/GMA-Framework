namespace Gma.Framework.Notifications.Api;

public static class NotificationSseItemKindNames
{
    public static string ToWireName(NotificationSseItemKind kind) =>
        kind switch
        {
            NotificationSseItemKind.Notification => "notification",
            NotificationSseItemKind.Heartbeat => "heartbeat",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Notification SSE item kind is invalid.")
        };

    public static bool TryParse(string? value, out NotificationSseItemKind kind)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out kind) &&
            kind is not NotificationSseItemKind.Unknown &&
            Enum.IsDefined(kind))
        {
            return true;
        }

        kind = normalized switch
        {
            "notification" => NotificationSseItemKind.Notification,
            "heartbeat" => NotificationSseItemKind.Heartbeat,
            _ => NotificationSseItemKind.Unknown
        };

        return kind is not NotificationSseItemKind.Unknown;
    }
}

namespace Gma.Framework.Notifications;

public enum NotificationDeliveryPolicy
{
    Unknown = 0,
    RespectPreferences = 1,
    Mandatory = 2
}

public static class NotificationDeliveryPolicies
{
    public static NotificationDeliveryPolicy Normalize(
        NotificationDeliveryPolicy policy,
        string parameterName = "deliveryPolicy") =>
        policy is NotificationDeliveryPolicy.RespectPreferences or NotificationDeliveryPolicy.Mandatory
            ? policy
            : throw new ArgumentOutOfRangeException(
                parameterName,
                policy,
                "Notification delivery policy must be respect-preferences or mandatory.");
}

namespace Gma.Framework.Notifications;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class NotificationVersionAttribute(int version) : Attribute
{
    public int Version { get; } = NotificationVersions.Normalize(version, nameof(version));

    public static NotificationVersionAttribute GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        if (!typeof(IUserNotificationPayload).IsAssignableFrom(payloadType))
        {
            throw new ArgumentException(
                $"Type '{payloadType.FullName}' must implement {nameof(IUserNotificationPayload)}.",
                nameof(payloadType));
        }

        return ModuleMetadataAttributeReader.GetRequired<NotificationVersionAttribute>(
            payloadType,
            "Notification payload");
    }
}

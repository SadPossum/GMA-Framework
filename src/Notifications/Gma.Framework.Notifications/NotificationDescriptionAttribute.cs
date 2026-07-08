namespace Gma.Framework.Notifications;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class NotificationDescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = NotificationDescriptions.Normalize(description, nameof(description));

    public static NotificationDescriptionAttribute GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        if (!typeof(IUserNotificationPayload).IsAssignableFrom(payloadType))
        {
            throw new ArgumentException(
                $"Type '{payloadType.FullName}' must implement {nameof(IUserNotificationPayload)}.",
                nameof(payloadType));
        }

        return ModuleMetadataAttributeReader.GetRequired<NotificationDescriptionAttribute>(
            payloadType,
            "Notification payload");
    }
}

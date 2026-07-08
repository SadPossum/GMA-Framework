namespace Gma.Framework.Notifications;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class NotificationNameAttribute(string name) : Attribute
{
    public string Name { get; } = NotificationNames.NormalizeName(name, nameof(name));

    public static NotificationNameAttribute GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        if (!typeof(IUserNotificationPayload).IsAssignableFrom(payloadType))
        {
            throw new ArgumentException(
                $"Type '{payloadType.FullName}' must implement {nameof(IUserNotificationPayload)}.",
                nameof(payloadType));
        }

        return ModuleMetadataAttributeReader.GetRequired<NotificationNameAttribute>(
            payloadType,
            "Notification payload");
    }
}

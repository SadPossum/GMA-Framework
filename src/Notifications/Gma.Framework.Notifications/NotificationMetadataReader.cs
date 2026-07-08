namespace Gma.Framework.Notifications;

using Gma.Framework.Modules;

public static class NotificationMetadataReader
{
    public static ModuleNotificationDescriptor CreateDescriptor(Type payloadType)
    {
        NotificationNameAttribute name = NotificationNameAttribute.GetRequired(payloadType);
        NotificationVersionAttribute version = NotificationVersionAttribute.GetRequired(payloadType);
        NotificationDescriptionAttribute description = NotificationDescriptionAttribute.GetRequired(payloadType);

        return new ModuleNotificationDescriptor(
            name.Name,
            description.Description,
            version.Version,
            ModuleMetadataAttributeReader.Read(payloadType).Items);
    }

    public static NotificationMetadata ReadRequired(Type payloadType)
    {
        NotificationNameAttribute name = NotificationNameAttribute.GetRequired(payloadType);
        NotificationVersionAttribute version = NotificationVersionAttribute.GetRequired(payloadType);
        NotificationDescriptionAttribute.GetRequired(payloadType);

        return new NotificationMetadata(name.Name, version.Version);
    }

    public readonly record struct NotificationMetadata(string Name, int Version);
}

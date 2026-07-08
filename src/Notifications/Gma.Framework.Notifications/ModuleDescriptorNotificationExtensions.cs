namespace Gma.Framework.Notifications;

using Gma.Framework.Modules;

public static class ModuleDescriptorNotificationExtensions
{
    public static ModuleDescriptorBuilder WithUserNotification<TPayload>(this ModuleDescriptorBuilder builder)
        where TPayload : IUserNotificationPayload
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithUserNotification(NotificationMetadataReader.CreateDescriptor(typeof(TPayload)));
    }

    public static ModuleDescriptorBuilder WithUserNotification(
        this ModuleDescriptorBuilder builder,
        ModuleNotificationDescriptor notification)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(notification);
        return builder.WithUserNotifications([notification]);
    }

    public static ModuleDescriptorBuilder WithUserNotifications(
        this ModuleDescriptorBuilder builder,
        IReadOnlyList<ModuleNotificationDescriptor> notifications)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithFeature(
            new ModuleNotificationsDescriptor(notifications),
            static (existing, incoming) =>
            {
                return new ModuleNotificationsDescriptor(existing
                    .Notifications
                    .Concat(incoming.Notifications)
                    .ToArray());
            });
    }

    public static IReadOnlyList<ModuleNotificationDescriptor> GetUserNotifications(this ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.GetFeature<ModuleNotificationsDescriptor>()?.Notifications ?? [];
    }
}

namespace Gma.Framework.Notifications;

using Gma.Framework.Modules;

public sealed record ModuleNotificationsDescriptor : ModuleDescriptorFeature
{
    public const string FeatureKey = "notifications.user";

    public ModuleNotificationsDescriptor(IReadOnlyList<ModuleNotificationDescriptor> notifications)
        : base(FeatureKey)
    {
        this.Notifications = ModuleMetadataGuards.CopyRequiredList(
            notifications,
            nameof(notifications));
        ModuleMetadataGuards.EnsureUnique(
            this.Notifications,
            notification => $"{notification.Name}:v{notification.Version}",
            "notification");
    }

    public IReadOnlyList<ModuleNotificationDescriptor> Notifications { get; }
}

namespace Gma.Framework.Notifications;

using Gma.Framework.Modules;

public sealed record ModuleNotificationDescriptor
{
    public ModuleNotificationDescriptor(
        string name,
        string description,
        int version,
        IReadOnlyList<ModuleMetadataItem>? metadata = null)
    {
        this.Name = NotificationNames.NormalizeName(name, nameof(name));
        this.Description = NotificationDescriptions.Normalize(description, nameof(description));
        this.Version = NotificationVersions.Normalize(version, nameof(version));
        this.Metadata = ModuleMetadataGuards.CopyOptionalList(metadata);
    }

    public string Name { get; }
    public string Description { get; }
    public int Version { get; }
    public IReadOnlyList<ModuleMetadataItem> Metadata { get; }
}

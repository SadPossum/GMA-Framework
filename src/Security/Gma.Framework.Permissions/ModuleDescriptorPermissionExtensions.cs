namespace Gma.Framework.Permissions;

using Gma.Framework.Modules;

public static class ModuleDescriptorPermissionExtensions
{
    public static ModuleDescriptorBuilder WithPermission(
        this ModuleDescriptorBuilder builder,
        ModulePermissionDescriptor permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permission);
        return builder.WithPermissions([permission]);
    }

    public static ModuleDescriptorBuilder WithPermissions(
        this ModuleDescriptorBuilder builder,
        IReadOnlyList<ModulePermissionDescriptor> permissions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithFeature(
            new ModulePermissionsDescriptor(permissions),
            static (existing, incoming) =>
            {
                return new ModulePermissionsDescriptor(existing
                    .Permissions
                    .Concat(incoming.Permissions)
                    .ToArray());
            });
    }

    public static IReadOnlyList<ModulePermissionDescriptor> GetPermissions(this ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.GetFeature<ModulePermissionsDescriptor>()?.Permissions ?? [];
    }
}

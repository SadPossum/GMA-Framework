namespace Gma.Framework.Permissions;

using Gma.Framework.Modules;

public sealed record ModulePermissionsDescriptor : ModuleDescriptorFeature
{
    public const string FeatureKey = "authorization.permissions";

    public ModulePermissionsDescriptor(IReadOnlyList<ModulePermissionDescriptor> permissions)
        : base(FeatureKey)
    {
        this.Permissions = ModuleMetadataGuards.CopyRequiredNonEmptyList(permissions, nameof(permissions));
        ModuleMetadataGuards.EnsureUnique(this.Permissions, permission => permission.Code, "permission");
    }

    public IReadOnlyList<ModulePermissionDescriptor> Permissions { get; }
}

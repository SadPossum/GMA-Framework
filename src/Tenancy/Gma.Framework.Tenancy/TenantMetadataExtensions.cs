namespace Gma.Framework.Tenancy;

using Gma.Framework.Modules;

public static class TenantMetadataExtensions
{
    public static bool IsTenantScoped(this IModuleMetadataProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Metadata.IsTenantScoped();
    }

    public static bool IsTenantScoped(this ModuleMetadataItems metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.Contains<TenantScopeMetadataItem>();
    }

    public static ModuleMetadataItem RequireTenantScopedMetadata(this ModuleMetadataItems metadata, string targetName)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        return metadata.Get<TenantScopeMetadataItem>() ?? throw new InvalidOperationException(
            $"{targetName} must declare {nameof(TenantScopedAttribute)} metadata.");
    }
}

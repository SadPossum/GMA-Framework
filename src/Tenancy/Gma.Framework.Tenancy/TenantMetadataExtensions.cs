namespace Gma.Framework.Tenancy;

using Gma.Framework.Modules;
using Gma.Framework.Scoping;

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
        return metadata.Contains<TenantScopeMetadataItem>() || metadata.Contains<ScopeMetadataItem>();
    }

    public static ModuleMetadataItem RequireTenantScopedMetadata(this ModuleMetadataItems metadata, string targetName)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        ModuleMetadataItem? scopeMetadata = metadata.Get<TenantScopeMetadataItem>() ?? (ModuleMetadataItem?)metadata.Get<ScopeMetadataItem>();
        return scopeMetadata ?? throw new InvalidOperationException(
            $"{targetName} must declare {nameof(TenantScopedAttribute)} metadata.");
    }
}

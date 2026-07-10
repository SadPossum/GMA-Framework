namespace Gma.Framework.Scoping;

using Gma.Framework.Modules;

public static class ScopeMetadataExtensions
{
    public static bool IsScopeAware(this IModuleMetadataProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Metadata.IsScopeAware();
    }

    public static bool IsScopeAware(this ModuleMetadataItems metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.Contains<ScopeMetadataItem>();
    }
}

namespace Gma.Framework.Caching;

using Gma.Framework.Modules;

public sealed record ModuleCacheEntriesDescriptor : ModuleDescriptorFeature
{
    public const string FeatureKey = "caching.entries";

    public ModuleCacheEntriesDescriptor(IReadOnlyList<ModuleCacheDescriptor> cacheEntries)
        : base(FeatureKey)
    {
        this.CacheEntries = ModuleMetadataGuards.CopyRequiredNonEmptyList(cacheEntries, nameof(cacheEntries));
        ModuleMetadataGuards.EnsureUnique(this.CacheEntries, cacheEntry => cacheEntry.Name, "cache entry");
    }

    public IReadOnlyList<ModuleCacheDescriptor> CacheEntries { get; }
}

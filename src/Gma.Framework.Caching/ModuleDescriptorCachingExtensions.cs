namespace Gma.Framework.Caching;

using Gma.Framework.Modules;

public static class ModuleDescriptorCachingExtensions
{
    public static ModuleDescriptorBuilder WithCacheEntry(
        this ModuleDescriptorBuilder builder,
        ModuleCacheDescriptor cacheEntry)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(cacheEntry);
        return builder.WithCacheEntries([cacheEntry]);
    }

    public static ModuleDescriptorBuilder WithCacheEntries(
        this ModuleDescriptorBuilder builder,
        IReadOnlyList<ModuleCacheDescriptor> cacheEntries)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithFeature(
            new ModuleCacheEntriesDescriptor(cacheEntries),
            static (existing, incoming) =>
            {
                return new ModuleCacheEntriesDescriptor(existing
                    .CacheEntries
                    .Concat(incoming.CacheEntries)
                    .ToArray());
            });
    }

    public static IReadOnlyList<ModuleCacheDescriptor> GetCacheEntries(this ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.GetFeature<ModuleCacheEntriesDescriptor>()?.CacheEntries ?? [];
    }
}

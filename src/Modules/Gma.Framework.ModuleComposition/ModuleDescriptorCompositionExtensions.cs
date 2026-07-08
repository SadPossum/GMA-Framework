namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Modules;

public static class ModuleDescriptorCompositionExtensions
{
    public static ModuleDescriptorBuilder WithProfile(
        this ModuleDescriptorBuilder builder,
        ModuleProfileDescriptor profile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);

        return builder.WithFeature(
            new ModuleCompositionDescriptor([profile]),
            static (existing, incoming) => new ModuleCompositionDescriptor([.. existing.Profiles, .. incoming.Profiles]));
    }

    public static ModuleDescriptorBuilder WithProfiles(
        this ModuleDescriptorBuilder builder,
        IReadOnlyList<ModuleProfileDescriptor> profiles)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithFeature(
            new ModuleCompositionDescriptor(profiles),
            static (existing, incoming) => new ModuleCompositionDescriptor([.. existing.Profiles, .. incoming.Profiles]));
    }

    public static IReadOnlyList<ModuleProfileDescriptor> GetCompositionProfiles(this ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.GetFeature<ModuleCompositionDescriptor>()?.Profiles ?? [];
    }
}

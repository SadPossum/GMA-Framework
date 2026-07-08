namespace Gma.Framework.Modules;

public sealed class ModuleDescriptorBuilder
{
    private readonly string name;
    private readonly List<ModuleDescriptorFeature> features = [];
    private string? schema;
    private string? adminSurfaceName;

    internal ModuleDescriptorBuilder(string name)
        => this.name = ModuleMetadataNaming.NormalizeModuleName(name, nameof(name));

    public string Name => this.name;

    public ModuleDescriptorBuilder WithSchema(string? value)
    {
        this.schema = value;
        return this;
    }

    public ModuleDescriptorBuilder WithAdminSurfaceName(string? value)
    {
        this.adminSurfaceName = value;
        return this;
    }

    public ModuleDescriptorBuilder WithFeature(ModuleDescriptorFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        if (this.features.Any(item => string.Equals(item.Key, feature.Key, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Module descriptor feature key '{feature.Key}' is already registered. Use the typed merge overload for additive feature metadata.",
                nameof(feature));
        }

        this.features.Add(feature);
        return this;
    }

    public ModuleDescriptorBuilder WithFeature<TFeature>(
        TFeature feature,
        Func<TFeature, TFeature, TFeature> merge)
        where TFeature : ModuleDescriptorFeature
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(merge);

        int existingIndex = this.features.FindIndex(item => string.Equals(item.Key, feature.Key, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            ModuleDescriptorFeature existing = this.features[existingIndex];
            if (existing.GetType() != feature.GetType() ||
                existing is not TFeature existingFeature)
            {
                throw new InvalidOperationException(
                    $"Module descriptor feature key '{feature.Key}' is already used by '{existing.GetType().FullName}' and cannot be merged with '{feature.GetType().FullName}'.");
            }

            TFeature merged = merge(existingFeature, feature);
            ArgumentNullException.ThrowIfNull(merged, nameof(merge));
            if (merged.GetType() != feature.GetType())
            {
                throw new InvalidOperationException(
                    $"Merged module descriptor feature type '{merged.GetType().FullName}' must match incoming type '{feature.GetType().FullName}'.");
            }

            if (!string.Equals(merged.Key, feature.Key, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Merged module descriptor feature key '{merged.Key}' must match incoming key '{feature.Key}'.");
            }

            this.features[existingIndex] = merged;
            return this;
        }

        this.features.Add(feature);
        return this;
    }

    public ModuleDescriptor Build() =>
        new(this.name, this.schema, this.adminSurfaceName, this.features);
}

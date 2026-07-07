namespace Gma.Framework.Modules;

public sealed record ModuleDescriptor
{
    internal ModuleDescriptor(
        string name,
        string? schema = null,
        string? adminSurfaceName = null,
        IReadOnlyList<ModuleDescriptorFeature>? features = null)
    {
        this.Name = ModuleMetadataNaming.NormalizeModuleName(name, nameof(name));
        this.Schema = string.IsNullOrWhiteSpace(schema)
            ? null
            : ModuleMetadataNaming.NormalizeModuleName(schema, nameof(schema));
        this.AdminSurfaceName = string.IsNullOrWhiteSpace(adminSurfaceName)
            ? null
            : ModuleMetadataNaming.NormalizeModuleName(adminSurfaceName, nameof(adminSurfaceName));
        this.Features = ModuleMetadataGuards.CopyOptionalList(features);

        ModuleMetadataGuards.EnsureUnique(this.Features, feature => feature.Key, "feature");

        ModuleDescriptorFeatureContext context = new(this.Name, this.Schema, this.AdminSurfaceName);
        foreach (ModuleDescriptorFeature feature in this.Features)
        {
            feature.Validate(context);
        }
    }

    public string Name { get; }
    public string? Schema { get; }
    public string? AdminSurfaceName { get; }
    public IReadOnlyList<ModuleDescriptorFeature> Features { get; }

    public TFeature? GetFeature<TFeature>()
        where TFeature : ModuleDescriptorFeature =>
        this.Features.OfType<TFeature>().SingleOrDefault();

    public IReadOnlyList<TFeature> GetFeatures<TFeature>()
        where TFeature : ModuleDescriptorFeature =>
        Array.AsReadOnly(this.Features.OfType<TFeature>().ToArray());

    public static ModuleDescriptorBuilder Create(string name) => new(name);

    public static ModuleDescriptor Empty(string name, string? schema = null, string? adminSurfaceName = null) =>
        Create(name)
            .WithSchema(schema)
            .WithAdminSurfaceName(adminSurfaceName)
            .Build();
}

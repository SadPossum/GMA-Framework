namespace Gma.Framework.Modules;

public sealed record ModuleDescriptorFeatureContext
{
    public ModuleDescriptorFeatureContext(string moduleName, string? schema, string? adminSurfaceName)
    {
        this.ModuleName = ModuleMetadataNaming.NormalizeModuleName(moduleName, nameof(moduleName));
        this.Schema = string.IsNullOrWhiteSpace(schema)
            ? null
            : ModuleMetadataNaming.NormalizeModuleName(schema, nameof(schema));
        this.AdminSurfaceName = string.IsNullOrWhiteSpace(adminSurfaceName)
            ? null
            : ModuleMetadataNaming.NormalizeModuleName(adminSurfaceName, nameof(adminSurfaceName));
    }

    public string ModuleName { get; }
    public string? Schema { get; }
    public string? AdminSurfaceName { get; }
}

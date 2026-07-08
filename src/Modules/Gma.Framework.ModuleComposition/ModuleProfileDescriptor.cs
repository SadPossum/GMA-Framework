namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Modules;
using Gma.Framework.Naming;

public sealed record ModuleProfileDescriptor
{
    public ModuleProfileDescriptor(
        string moduleName,
        string profileName,
        IReadOnlyList<ProvidedCompositionFeature>? provides = null,
        IReadOnlyList<RequiredCompositionFeature>? requires = null,
        IReadOnlyList<RequiredCompositionModule>? requiredModules = null,
        string? displayName = null,
        string? description = null)
    {
        this.ModuleName = SharedModuleNames.Normalize(moduleName, nameof(moduleName));
        this.ProfileName = SharedNameSegments.NormalizeKebabSegment(profileName, "profile name", nameof(profileName));
        this.Provides = ModuleMetadataGuards.CopyOptionalList(provides);
        this.Requires = ModuleMetadataGuards.CopyOptionalList(requires);
        this.RequiredModules = ModuleMetadataGuards.CopyOptionalList(requiredModules);
        this.DisplayName = CompositionText.OptionalSafeText(displayName, nameof(displayName)) ??
                           $"{this.ModuleName}/{this.ProfileName}";
        this.Description = CompositionText.OptionalSafeText(description, nameof(description));

        ModuleMetadataGuards.EnsureUnique(this.Provides, feature => feature.Id.Value, "provided composition feature");
        ModuleMetadataGuards.EnsureUnique(this.Requires, feature => feature.Id.Value, "required composition feature");
        ModuleMetadataGuards.EnsureUnique(this.RequiredModules, module => module.ModuleName, "required composition module");
    }

    public string ModuleName { get; }
    public string ProfileName { get; }
    public IReadOnlyList<ProvidedCompositionFeature> Provides { get; }
    public IReadOnlyList<RequiredCompositionFeature> Requires { get; }
    public IReadOnlyList<RequiredCompositionModule> RequiredModules { get; }
    public string DisplayName { get; }
    public string? Description { get; }

    public string ProviderName => $"{this.ModuleName}/{this.ProfileName}";
}

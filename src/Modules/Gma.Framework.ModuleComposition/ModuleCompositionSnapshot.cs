namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Modules;

public sealed record ModuleCompositionSnapshot
{
    public ModuleCompositionSnapshot(
        IReadOnlyList<SelectedModuleProfile>? selectedProfiles = null,
        IReadOnlyList<ProvidedCompositionFeature>? providedFeatures = null,
        IReadOnlyList<RequiredCompositionFeature>? requiredFeatures = null,
        IReadOnlyList<RequiredCompositionModule>? requiredModules = null)
    {
        this.SelectedProfiles = ModuleMetadataGuards.CopyOptionalList(selectedProfiles);
        this.ProvidedFeatures = ModuleMetadataGuards.CopyOptionalList(providedFeatures);
        this.RequiredFeatures = ModuleMetadataGuards.CopyOptionalList(requiredFeatures);
        this.RequiredModules = ModuleMetadataGuards.CopyOptionalList(requiredModules);
    }

    public IReadOnlyList<SelectedModuleProfile> SelectedProfiles { get; }
    public IReadOnlyList<ProvidedCompositionFeature> ProvidedFeatures { get; }
    public IReadOnlyList<RequiredCompositionFeature> RequiredFeatures { get; }
    public IReadOnlyList<RequiredCompositionModule> RequiredModules { get; }
}

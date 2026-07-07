namespace Gma.Framework.ModuleComposition;

public sealed record SelectedModuleProfile
{
    public SelectedModuleProfile(ModuleProfileDescriptor profile, string? selectedBy = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        this.Profile = profile;
        this.SelectedBy = CompositionText.OptionalSafeText(selectedBy, nameof(selectedBy)) ?? profile.ProviderName;
    }

    public ModuleProfileDescriptor Profile { get; }
    public string SelectedBy { get; }
}

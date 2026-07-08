namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Modules;

public sealed record ModuleCompositionDescriptor : ModuleDescriptorFeature
{
    public const string FeatureKey = "composition.profiles";

    public ModuleCompositionDescriptor(IReadOnlyList<ModuleProfileDescriptor> profiles)
        : base(FeatureKey)
    {
        this.Profiles = ModuleMetadataGuards.CopyRequiredNonEmptyList(profiles, nameof(profiles));
        ModuleMetadataGuards.EnsureUnique(this.Profiles, profile => profile.ProfileName, "composition profile");
    }

    public IReadOnlyList<ModuleProfileDescriptor> Profiles { get; }

    public override void Validate(ModuleDescriptorFeatureContext context)
    {
        base.Validate(context);

        foreach (ModuleProfileDescriptor profile in this.Profiles)
        {
            if (!string.Equals(profile.ModuleName, context.ModuleName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Composition profile '{profile.ProfileName}' belongs to module '{profile.ModuleName}', but descriptor module is '{context.ModuleName}'.");
            }
        }
    }
}

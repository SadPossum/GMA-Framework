namespace Gma.Framework.ModuleComposition;

public sealed record RequiredCompositionFeature
{
    public RequiredCompositionFeature(
        CompositionFeatureId id,
        string owner,
        bool optional = false,
        string? reason = null)
    {
        CompositionText.EnsureFeatureIdIsNotDefault(id, nameof(id));

        this.Id = id;
        this.Owner = CompositionText.RequireSafeText(owner, nameof(owner));
        this.Optional = optional;
        this.Reason = CompositionText.OptionalSafeText(reason, nameof(reason));
    }

    public CompositionFeatureId Id { get; }
    public string Owner { get; }
    public bool Optional { get; }
    public string? Reason { get; }
}

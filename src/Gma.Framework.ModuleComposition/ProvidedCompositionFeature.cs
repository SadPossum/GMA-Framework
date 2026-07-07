namespace Gma.Framework.ModuleComposition;

public sealed record ProvidedCompositionFeature
{
    public ProvidedCompositionFeature(
        CompositionFeatureId id,
        string provider,
        string? description = null,
        bool allowMultipleProviders = false)
    {
        CompositionText.EnsureFeatureIdIsNotDefault(id, nameof(id));

        this.Id = id;
        this.Provider = new CompositionProvider(provider);
        this.Description = CompositionText.OptionalSafeText(description, nameof(description));
        this.AllowMultipleProviders = allowMultipleProviders;
    }

    public CompositionFeatureId Id { get; }
    public CompositionProvider Provider { get; }
    public string? Description { get; }
    public bool AllowMultipleProviders { get; }
}

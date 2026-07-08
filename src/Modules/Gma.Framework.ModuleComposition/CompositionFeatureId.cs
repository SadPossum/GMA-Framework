namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Modules;

public readonly record struct CompositionFeatureId
{
    private readonly string? value;

    public CompositionFeatureId(string value)
        => this.value = ModuleMetadataNaming.NormalizeFeatureKey(value, nameof(value));

    public string Value => this.value ?? string.Empty;

    public override string ToString() => this.Value;
}

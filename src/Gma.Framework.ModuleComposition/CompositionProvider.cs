namespace Gma.Framework.ModuleComposition;

public readonly record struct CompositionProvider
{
    public CompositionProvider(string value) =>
        this.Value = CompositionText.RequireSafeText(value, nameof(value));

    public string Value { get; }

    public override string ToString() => this.Value;
}

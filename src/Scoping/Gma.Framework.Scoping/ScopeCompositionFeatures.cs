namespace Gma.Framework.Scoping;

using Gma.Framework.ModuleComposition;

public static class ScopeCompositionFeatures
{
    public static readonly CompositionFeatureId Context = new("scoping.context");

    public static ProvidedCompositionFeature ContextProvided(string provider) =>
        new(
            Context,
            provider,
            "Scope context services for scope-aware module profiles.",
            allowMultipleProviders: true);

    public static RequiredCompositionFeature ContextRequired(
        string owner,
        string? reason = null,
        bool optional = false) =>
        new(Context, owner, optional, reason);
}

namespace Gma.Framework.Tenancy;

using Gma.Framework.ModuleComposition;

public static class TenancyCompositionFeatures
{
    public static readonly CompositionFeatureId Context = new("tenancy.context");
    public static readonly CompositionFeatureId HeaderResolution = new("tenancy.header-resolution");

    public static ProvidedCompositionFeature ContextProvided(string provider) =>
        new(
            Context,
            provider,
            "Tenant context services for tenant-scoped module profiles.",
            allowMultipleProviders: true);

    public static ProvidedCompositionFeature HeaderResolutionProvided(string provider) =>
        new(
            HeaderResolution,
            provider,
            "Tenant context resolution from inbound HTTP tenant headers.");
}

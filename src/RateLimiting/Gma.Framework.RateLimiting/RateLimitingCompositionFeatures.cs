namespace Gma.Framework.RateLimiting;

using Gma.Framework.ModuleComposition;

public static class RateLimitingCompositionFeatures
{
    public static readonly CompositionFeatureId Provider = new("rate-limiting.provider");
    public static readonly CompositionFeatureId DistributedProvider = new("rate-limiting.distributed-provider");

    public static ProvidedCompositionFeature ProviderProvided(string provider) =>
        new(Provider, provider, "An atomic multi-partition rate-limit provider is registered.");

    public static ProvidedCompositionFeature DistributedProviderProvided(string provider) =>
        new(DistributedProvider, provider, "A distributed atomic multi-partition rate-limit provider is registered.");

    public static RequiredCompositionFeature ProviderRequired(
        string owner,
        string? reason = null,
        bool optional = false) =>
        new(Provider, owner, optional, reason);

    public static RequiredCompositionFeature DistributedProviderRequired(
        string owner,
        string? reason = null,
        bool optional = false) =>
        new(DistributedProvider, owner, optional, reason);
}

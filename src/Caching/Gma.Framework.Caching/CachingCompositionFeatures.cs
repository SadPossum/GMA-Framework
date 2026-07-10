namespace Gma.Framework.Caching;

using Gma.Framework.ModuleComposition;

public static class CachingCompositionFeatures
{
    public static readonly CompositionFeatureId Application = new("caching.application");
    public static readonly CompositionFeatureId Invalidation = new("caching.invalidation");
    public static readonly CompositionFeatureId ScopeContext = new("caching.scope-context");
    public static readonly CompositionFeatureId CqrsInvalidation = new("caching.cqrs-invalidation");
    public static readonly CompositionFeatureId Redis = new("caching.redis");

    public static ProvidedCompositionFeature ApplicationProvided(string provider) =>
        new(Application, provider, "Application cache-aside services are registered.");

    public static ProvidedCompositionFeature InvalidationProvided(string provider) =>
        new(Invalidation, provider, "Deferred cache invalidation queue services are registered.");

    public static ProvidedCompositionFeature ScopeContextProvided(string provider) =>
        new(ScopeContext, provider, "Scope-aware cache keys resolve through scope context.");

    public static ProvidedCompositionFeature CqrsInvalidationProvided(string provider) =>
        new(CqrsInvalidation, provider, "CQRS command pipeline flushes deferred cache invalidations after successful commits.");

    public static ProvidedCompositionFeature RedisProvided(string provider) =>
        new(Redis, provider, "Redis is registered as the distributed cache backend.");

    public static RequiredCompositionFeature ApplicationRequired(string owner, string? reason = null, bool optional = false) =>
        new(Application, owner, optional, reason);

    public static RequiredCompositionFeature InvalidationRequired(string owner, string? reason = null, bool optional = false) =>
        new(Invalidation, owner, optional, reason);

    public static RequiredCompositionFeature ScopeContextRequired(string owner, string? reason = null, bool optional = false) =>
        new(ScopeContext, owner, optional, reason);

    public static RequiredCompositionFeature CqrsInvalidationRequired(string owner, string? reason = null, bool optional = false) =>
        new(CqrsInvalidation, owner, optional, reason);

    public static RequiredCompositionFeature RedisRequired(string owner, string? reason = null, bool optional = false) =>
        new(Redis, owner, optional, reason);
}

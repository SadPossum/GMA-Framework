namespace Gma.Framework.Tenancy.Messaging;

using Gma.Framework.ModuleComposition;

public static class TenancyMessagingCompositionFeatures
{
    public static readonly CompositionFeatureId TenantEventScope = new("tenancy.messaging-event-scope");
    public static readonly CompositionFeatureId TenantConsumerContext = new("tenancy.messaging-consumer-context");

    public static ProvidedCompositionFeature TenantEventScopeProvided(string provider) =>
        new(TenantEventScope, provider, "Tenant-aware integration events resolve a generic messaging scope id.");

    public static ProvidedCompositionFeature TenantConsumerContextProvided(string provider) =>
        new(TenantConsumerContext, provider, "Tenant-aware integration event consumers set tenant context before handler execution.");

    public static RequiredCompositionFeature TenantEventScopeRequired(string owner, string? reason = null, bool optional = false) =>
        new(TenantEventScope, owner, optional, reason);

    public static RequiredCompositionFeature TenantConsumerContextRequired(string owner, string? reason = null, bool optional = false) =>
        new(TenantConsumerContext, owner, optional, reason);
}

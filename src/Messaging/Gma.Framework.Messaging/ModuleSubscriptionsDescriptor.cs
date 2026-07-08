namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

public sealed record ModuleSubscriptionsDescriptor : ModuleDescriptorFeature
{
    public const string FeatureKey = "messaging.subscriptions";

    public ModuleSubscriptionsDescriptor(IReadOnlyList<ModuleSubscriptionDescriptor> subscriptions)
        : base(FeatureKey)
    {
        this.Subscriptions = ModuleMetadataGuards.CopyRequiredNonEmptyList(subscriptions, nameof(subscriptions));
        ModuleMetadataGuards.EnsureUnique(
            this.Subscriptions,
            subscription => $"{subscription.ProducerModule}.{subscription.HandlerName}",
            "subscription handler");
    }

    public IReadOnlyList<ModuleSubscriptionDescriptor> Subscriptions { get; }
}

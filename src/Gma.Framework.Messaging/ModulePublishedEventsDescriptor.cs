namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

public sealed record ModulePublishedEventsDescriptor : ModuleDescriptorFeature
{
    public const string FeatureKey = "messaging.published-events";

    public ModulePublishedEventsDescriptor(IReadOnlyList<ModuleIntegrationEventDescriptor> publishedEvents)
        : base(FeatureKey)
    {
        this.PublishedEvents = ModuleMetadataGuards.CopyRequiredNonEmptyList(publishedEvents, nameof(publishedEvents));
        ModuleMetadataGuards.EnsureUnique(this.PublishedEvents, publishedEvent => publishedEvent.Subject, "published event subject");
    }

    public IReadOnlyList<ModuleIntegrationEventDescriptor> PublishedEvents { get; }

    public override void Validate(ModuleDescriptorFeatureContext context)
    {
        base.Validate(context);

        foreach (ModuleIntegrationEventDescriptor publishedEvent in this.PublishedEvents)
        {
            string expectedSubject = IntegrationEventNaming.CreateSubject(
                publishedEvent.SubjectPrefix,
                context.ModuleName,
                publishedEvent.EventType,
                publishedEvent.Version);

            if (!string.Equals(publishedEvent.Subject, expectedSubject, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Published event '{publishedEvent.EventType}' subject must match the module name and event version.",
                    nameof(context));
            }
        }
    }
}

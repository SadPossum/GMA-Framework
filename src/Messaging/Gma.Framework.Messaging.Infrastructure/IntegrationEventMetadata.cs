namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Messaging;

public static class IntegrationEventMetadata
{
    public const string EventIdRequiredReason = "event-id-required";
    public const string EventNameRequiredReason = "event-name-required";
    public const string EventNameInvalidReason = "event-name-invalid";
    public const string EventVersionInvalidReason = "event-version-invalid";

    public static void ValidateForPublishing(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.EventId == Guid.Empty)
        {
            throw new ArgumentException("An integration event id is required.", nameof(integrationEvent));
        }

        _ = IntegrationEventNaming.NormalizeEventName(integrationEvent.EventName, nameof(IIntegrationEvent.EventName));
        ArgumentOutOfRangeException.ThrowIfLessThan(integrationEvent.Version, 1, nameof(IIntegrationEvent.Version));
    }

    public static bool TryGetInvalidReason(IIntegrationEvent integrationEvent, out string reason)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.EventId == Guid.Empty)
        {
            reason = EventIdRequiredReason;
            return true;
        }

        if (string.IsNullOrWhiteSpace(integrationEvent.EventName))
        {
            reason = EventNameRequiredReason;
            return true;
        }

        if (!IntegrationEventNaming.TryNormalizeEventName(integrationEvent.EventName, out _))
        {
            reason = EventNameInvalidReason;
            return true;
        }

        if (integrationEvent.Version < 1)
        {
            reason = EventVersionInvalidReason;
            return true;
        }

        reason = string.Empty;
        return false;
    }
}

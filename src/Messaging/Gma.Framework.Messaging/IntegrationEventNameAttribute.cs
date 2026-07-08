namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IntegrationEventNameAttribute(string eventName) : Attribute
{
    public string EventName { get; } = IntegrationEventNaming.NormalizeEventName(eventName, nameof(eventName));

    public static IntegrationEventNameAttribute GetRequired(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
        {
            throw new ArgumentException(
                $"Type '{eventType.FullName}' must implement {nameof(IIntegrationEvent)}.",
                nameof(eventType));
        }

        return ModuleMetadataAttributeReader.GetRequired<IntegrationEventNameAttribute>(
            eventType,
            "Integration event");
    }
}

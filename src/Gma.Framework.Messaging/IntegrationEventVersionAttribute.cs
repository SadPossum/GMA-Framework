namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IntegrationEventVersionAttribute(int version) : Attribute
{
    public int Version { get; } = version > 0
        ? version
        : throw new ArgumentOutOfRangeException(nameof(version), version, "Integration event version must be positive.");

    public static IntegrationEventVersionAttribute GetRequired(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
        {
            throw new ArgumentException(
                $"Type '{eventType.FullName}' must implement {nameof(IIntegrationEvent)}.",
                nameof(eventType));
        }

        return ModuleMetadataAttributeReader.GetRequired<IntegrationEventVersionAttribute>(
            eventType,
            "Integration event");
    }
}

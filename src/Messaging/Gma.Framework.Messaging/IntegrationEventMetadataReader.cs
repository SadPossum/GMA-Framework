namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

internal static class IntegrationEventMetadataReader
{
    public static IntegrationEventMetadata ReadRequired(Type eventType)
    {
        IntegrationEventNameAttribute name = IntegrationEventNameAttribute.GetRequired(eventType);
        IntegrationEventVersionAttribute version = IntegrationEventVersionAttribute.GetRequired(eventType);
        ModuleMetadataItems metadata = ModuleMetadataAttributeReader.Read(eventType);

        return new IntegrationEventMetadata(name.EventName, version.Version, metadata);
    }

    internal readonly record struct IntegrationEventMetadata(
        string EventName,
        int Version,
        ModuleMetadataItems Metadata);
}

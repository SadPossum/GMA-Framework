namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IntegrationEventHandlerAttribute(string handlerName) : Attribute
{
    public string HandlerName { get; } = IntegrationEventNaming.NormalizeHandlerName(handlerName, nameof(handlerName));
    public bool RequiresExplicitProducerBinding { get; init; }

    public static IntegrationEventHandlerAttribute GetRequired(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        return ModuleMetadataAttributeReader.GetRequired<IntegrationEventHandlerAttribute>(
            handlerType,
            "Integration event handler");
    }
}

namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TaskPayloadVersionAttribute(int payloadVersion) : Attribute
{
    public int PayloadVersion { get; } = payloadVersion > 0
        ? payloadVersion
        : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Task payload version must be positive.");

    public static TaskPayloadVersionAttribute GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        if (!typeof(ITaskPayload).IsAssignableFrom(payloadType))
        {
            throw new ArgumentException(
                $"Type '{payloadType.FullName}' must implement {nameof(ITaskPayload)}.",
                nameof(payloadType));
        }

        return ModuleMetadataAttributeReader.GetRequired<TaskPayloadVersionAttribute>(
            payloadType,
            "Task payload");
    }
}

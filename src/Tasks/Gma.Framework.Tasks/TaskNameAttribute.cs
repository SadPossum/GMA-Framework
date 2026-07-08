namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TaskNameAttribute(string taskName) : Attribute
{
    public string TaskName { get; } = TaskNames.NormalizeTaskName(taskName, nameof(taskName));

    public static TaskNameAttribute GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        if (!typeof(ITaskPayload).IsAssignableFrom(payloadType))
        {
            throw new ArgumentException(
                $"Type '{payloadType.FullName}' must implement {nameof(ITaskPayload)}.",
                nameof(payloadType));
        }

        return ModuleMetadataAttributeReader.GetRequired<TaskNameAttribute>(
            payloadType,
            "Task payload");
    }
}

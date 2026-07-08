namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TaskDescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = NormalizeDescription(description);

    public static TaskDescriptionAttribute GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        return ModuleMetadataAttributeReader.GetRequired<TaskDescriptionAttribute>(
            payloadType,
            "Task payload");
    }

    internal static string NormalizeDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        string normalized = description.Trim();
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Task description cannot contain control characters.", nameof(description));
        }

        return normalized;
    }
}

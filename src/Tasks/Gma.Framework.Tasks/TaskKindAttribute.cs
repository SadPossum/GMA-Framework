namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TaskKindAttribute(ModuleTaskKind kind) : Attribute
{
    public ModuleTaskKind Kind { get; } = Normalize(kind, nameof(kind));

    public static TaskKindAttribute GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        return ModuleMetadataAttributeReader.GetRequired<TaskKindAttribute>(
            payloadType,
            "Task payload");
    }

    internal static ModuleTaskKind Normalize(ModuleTaskKind kind, string parameterName) =>
        kind is ModuleTaskKind.Unknown || !Enum.IsDefined(kind)
            ? throw new ArgumentException("Task kind must be a known non-unknown value.", parameterName)
            : kind;
}

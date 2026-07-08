namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

internal static class TaskPayloadMetadataReader
{
    public static ModuleTaskDescriptor CreateDescriptor(Type payloadType)
    {
        TaskNameAttribute taskName = TaskNameAttribute.GetRequired(payloadType);
        TaskPayloadVersionAttribute payloadVersion = TaskPayloadVersionAttribute.GetRequired(payloadType);
        TaskDescriptionAttribute description = TaskDescriptionAttribute.GetRequired(payloadType);
        TaskKindAttribute kind = TaskKindAttribute.GetRequired(payloadType);

        return new ModuleTaskDescriptor(
            taskName.TaskName,
            description.Description,
            kind.Kind,
            SupportsTaskControlAttribute.IsDefinedOn(payloadType),
            TaskWorkerGroupAttribute.GetOrDefault(payloadType),
            payloadVersion.PayloadVersion,
            ModuleMetadataAttributeReader.Read(payloadType).Items);
    }

    public static TaskHandlerRegistration CreateRegistration<TPayload, THandler>(string moduleName)
        where TPayload : ITaskPayload
        where THandler : class, ITaskHandler<TPayload>
    {
        TaskNameAttribute taskName = TaskNameAttribute.GetRequired(typeof(TPayload));
        TaskPayloadVersionAttribute payloadVersion = TaskPayloadVersionAttribute.GetRequired(typeof(TPayload));
        TaskDescriptionAttribute.GetRequired(typeof(TPayload));
        TaskKindAttribute kind = TaskKindAttribute.GetRequired(typeof(TPayload));

        return TaskHandlerRegistration.Create<TPayload, THandler>(
            moduleName,
            taskName.TaskName,
            TaskWorkerGroupAttribute.GetOrDefault(typeof(TPayload)),
            payloadVersion.PayloadVersion,
            kind.Kind,
            SupportsTaskControlAttribute.IsDefinedOn(typeof(TPayload)),
            ModuleMetadataAttributeReader.Read(typeof(TPayload)).Items);
    }
}

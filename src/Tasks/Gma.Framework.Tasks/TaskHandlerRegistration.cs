namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

public sealed class TaskHandlerRegistration : IModuleMetadataProvider
{
    private TaskHandlerRegistration(
        string moduleName,
        string taskName,
        string workerGroup,
        Type payloadType,
        Type handlerType,
        ModuleTaskKind kind,
        int payloadVersion,
        bool supportsControlMessages,
        ModuleMetadataItems metadata)
    {
        this.ModuleName = moduleName;
        this.TaskName = taskName;
        this.WorkerGroup = workerGroup;
        this.PayloadType = payloadType;
        this.HandlerType = handlerType;
        this.Kind = kind;
        this.PayloadVersion = payloadVersion;
        this.SupportsControlMessages = supportsControlMessages;
        this.Metadata = metadata;
    }

    public string ModuleName { get; }
    public string TaskName { get; }
    public string WorkerGroup { get; }
    public Type PayloadType { get; }
    public Type HandlerType { get; }
    public ModuleTaskKind Kind { get; }
    public int PayloadVersion { get; }
    public bool SupportsControlMessages { get; }
    public ModuleMetadataItems Metadata { get; }

    public static TaskHandlerRegistration Create<TPayload, THandler>(string moduleName)
        where TPayload : ITaskPayload
        where THandler : class, ITaskHandler<TPayload>
    {
        return TaskPayloadMetadataReader.CreateRegistration<TPayload, THandler>(moduleName);
    }

    public static TaskHandlerRegistration Create<TPayload, THandler>(
        string moduleName,
        string taskName,
        string workerGroup = TaskWorkerGroups.Default,
        int payloadVersion = 1,
        ModuleTaskKind kind = ModuleTaskKind.OneShot,
        bool supportsControlMessages = false,
        IReadOnlyList<ModuleMetadataItem>? metadata = null)
        where TPayload : ITaskPayload
        where THandler : class, ITaskHandler<TPayload>
    {
        return new(
            TaskNames.NormalizeModuleName(moduleName, nameof(moduleName)),
            TaskNames.NormalizeTaskName(taskName, nameof(taskName)),
            TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup)),
            typeof(TPayload),
            typeof(THandler),
            TaskKindAttribute.Normalize(kind, nameof(kind)),
            payloadVersion > 0
                ? payloadVersion
                : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Task payload version must be positive."),
            supportsControlMessages,
            ModuleMetadataItems.Create(metadata));
    }
}

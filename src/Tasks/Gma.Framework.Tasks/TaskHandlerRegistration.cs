namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

public sealed class TaskHandlerRegistration : IModuleMetadataProvider
{
    public static TimeSpan MaximumSupportedHandlerTimeout { get; } =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    private TaskHandlerRegistration(
        string moduleName,
        string taskName,
        string workerGroup,
        Type payloadType,
        Type handlerType,
        ModuleTaskKind kind,
        int payloadVersion,
        bool supportsControlMessages,
        TimeSpan? handlerTimeout,
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
        this.HandlerTimeout = handlerTimeout;
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
    public TimeSpan? HandlerTimeout { get; }
    public ModuleMetadataItems Metadata { get; }

    public static TaskHandlerRegistration Create<TPayload, THandler>(
        string moduleName,
        TimeSpan? handlerTimeout = null)
        where TPayload : ITaskPayload
        where THandler : class, ITaskHandler<TPayload>
    {
        return TaskPayloadMetadataReader.CreateRegistration<TPayload, THandler>(
            moduleName,
            handlerTimeout);
    }

    public static TaskHandlerRegistration Create<TPayload, THandler>(
        string moduleName,
        string taskName,
        string workerGroup = TaskWorkerGroups.Default,
        int payloadVersion = 1,
        ModuleTaskKind kind = ModuleTaskKind.OneShot,
        bool supportsControlMessages = false,
        IReadOnlyList<ModuleMetadataItem>? metadata = null,
        TimeSpan? handlerTimeout = null)
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
            NormalizeHandlerTimeout(handlerTimeout),
            ModuleMetadataItems.Create(metadata));
    }

    private static TimeSpan? NormalizeHandlerTimeout(TimeSpan? handlerTimeout)
    {
        if (handlerTimeout is null)
        {
            return null;
        }

        return handlerTimeout > TimeSpan.Zero &&
            handlerTimeout <= MaximumSupportedHandlerTimeout
                ? handlerTimeout
                : throw new ArgumentOutOfRangeException(
                    nameof(handlerTimeout),
                    handlerTimeout,
                    $"Task handler timeout must be positive and no greater than {MaximumSupportedHandlerTimeout}.");
    }
}

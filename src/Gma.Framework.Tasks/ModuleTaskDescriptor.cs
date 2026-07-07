namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

public sealed record ModuleTaskDescriptor : IModuleMetadataProvider
{
    public ModuleTaskDescriptor(
        string name,
        string description,
        ModuleTaskKind kind,
        bool supportsControlMessages,
        string workerGroup = TaskWorkerGroups.Default,
        int payloadVersion = 1,
        IReadOnlyList<ModuleMetadataItem>? metadata = null)
    {
        this.Name = TaskNames.NormalizeTaskName(name, nameof(name));
        this.Description = TaskDescriptionAttribute.NormalizeDescription(description);
        this.Kind = TaskKindAttribute.Normalize(kind, nameof(kind));
        this.SupportsControlMessages = supportsControlMessages;
        this.WorkerGroup = TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup));
        this.PayloadVersion = payloadVersion > 0
            ? payloadVersion
            : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Task payload version must be positive.");
        this.Metadata = ModuleMetadataItems.Create(metadata);
    }

    public string Name { get; }
    public string Description { get; }
    public ModuleTaskKind Kind { get; }
    public bool SupportsControlMessages { get; }
    public string WorkerGroup { get; }
    public int PayloadVersion { get; }
    public ModuleMetadataItems Metadata { get; }
}

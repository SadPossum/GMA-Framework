namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

public sealed record ModuleTasksDescriptor : ModuleDescriptorFeature
{
    public const string FeatureKey = "tasks.handlers";

    public ModuleTasksDescriptor(IReadOnlyList<ModuleTaskDescriptor> tasks)
        : base(FeatureKey)
    {
        this.Tasks = ModuleMetadataGuards.CopyRequiredNonEmptyList(tasks, nameof(tasks));
        ModuleMetadataGuards.EnsureUnique(this.Tasks, task => $"{task.Name}.v{task.PayloadVersion}", "task");
    }

    public IReadOnlyList<ModuleTaskDescriptor> Tasks { get; }
}

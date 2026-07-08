namespace Gma.Framework.Tasks;

using Gma.Framework.Modules;

public static class ModuleDescriptorTaskExtensions
{
    public static ModuleDescriptorBuilder WithTask<TPayload>(this ModuleDescriptorBuilder builder)
        where TPayload : ITaskPayload
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithTask(TaskPayloadMetadataReader.CreateDescriptor(typeof(TPayload)));
    }

    public static ModuleDescriptorBuilder WithTask(
        this ModuleDescriptorBuilder builder,
        ModuleTaskDescriptor task)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(task);
        return builder.WithTasks([task]);
    }

    public static ModuleDescriptorBuilder WithTasks(
        this ModuleDescriptorBuilder builder,
        IReadOnlyList<ModuleTaskDescriptor> tasks)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithFeature(
            new ModuleTasksDescriptor(tasks),
            static (existing, incoming) =>
            {
                return new ModuleTasksDescriptor(existing
                    .Tasks
                    .Concat(incoming.Tasks)
                    .ToArray());
            });
    }

    public static IReadOnlyList<ModuleTaskDescriptor> GetTasks(this ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.GetFeature<ModuleTasksDescriptor>()?.Tasks ?? [];
    }
}

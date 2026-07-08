namespace Gma.Framework.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Gma.Framework.Modules;

public static class TaskHandlerServiceCollectionExtensions
{
    public static IServiceCollection AddTaskHandler<TPayload, THandler>(
        this IServiceCollection services,
        string moduleName)
        where TPayload : ITaskPayload
        where THandler : class, ITaskHandler<TPayload>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        TaskHandlerRegistration registration = TaskHandlerRegistration.Create<TPayload, THandler>(moduleName);

        services.TryAddSingleton<ITaskHandlerRegistry, TaskHandlerRegistry>();
        services.TryAddScoped<THandler>();
        AddRegistration(services, registration);

        return services;
    }

    public static IServiceCollection AddTaskHandler<TPayload, THandler>(
        this IServiceCollection services,
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
        ArgumentNullException.ThrowIfNull(services);

        TaskHandlerRegistration registration = TaskHandlerRegistration.Create<TPayload, THandler>(
            moduleName,
            taskName,
            workerGroup,
            payloadVersion,
            kind,
            supportsControlMessages,
            metadata);

        services.TryAddSingleton<ITaskHandlerRegistry, TaskHandlerRegistry>();
        services.TryAddScoped<THandler>();
        AddRegistration(services, registration);

        return services;
    }

    private static void AddRegistration(IServiceCollection services, TaskHandlerRegistration registration)
    {
        foreach (TaskHandlerRegistration existing in services
                     .Where(descriptor => descriptor.ServiceType == typeof(TaskHandlerRegistration))
                     .Select(descriptor => descriptor.ImplementationInstance)
                     .OfType<TaskHandlerRegistration>())
        {
            if (!string.Equals(existing.ModuleName, registration.ModuleName, StringComparison.Ordinal) ||
                !string.Equals(existing.TaskName, registration.TaskName, StringComparison.Ordinal) ||
                existing.PayloadVersion != registration.PayloadVersion)
            {
                continue;
            }

            if (IsSameRegistration(existing, registration))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Task handler '{registration.ModuleName}.{registration.TaskName}.v{registration.PayloadVersion}' is already registered with different metadata.");
        }

        services.AddSingleton(registration);
    }

    private static bool IsSameRegistration(
        TaskHandlerRegistration existing,
        TaskHandlerRegistration registration) =>
        string.Equals(existing.WorkerGroup, registration.WorkerGroup, StringComparison.Ordinal) &&
        existing.PayloadType == registration.PayloadType &&
        existing.HandlerType == registration.HandlerType &&
        existing.Kind == registration.Kind &&
        existing.PayloadVersion == registration.PayloadVersion &&
        existing.SupportsControlMessages == registration.SupportsControlMessages &&
        existing.Metadata == registration.Metadata;
}

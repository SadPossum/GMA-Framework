namespace Gma.Framework.Tasks.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Tasks;

public static class TaskWorkerRuntimeDependencyInjection
{
    public static IHostApplicationBuilder AddTaskInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddRuntimeInfrastructure();

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TaskInfrastructureRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<TaskInfrastructureRegistrationMarker>();
        builder.ProvideFeature(TasksCompositionFeatures.InfrastructureProvided("Gma.Framework.Tasks.Infrastructure"));
        builder.Services.TryAddScoped<ITaskControlLoop, TaskControlLoop>();
        builder.Services.TryAddSingleton<TaskMetricsSnapshotStore>();
        builder.Services.TryAddSingleton<TaskMetrics>();

        return builder;
    }

    public static IHostApplicationBuilder AddTaskWorkerRuntime(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddTaskInfrastructure();
        builder.ProvideFeature(TasksCompositionFeatures.WorkerProvided("Gma.Framework.Tasks.Infrastructure"));
        builder.RequireFeature(TasksCompositionFeatures.RunStoreRequired(
            "Gma.Framework.Tasks.Infrastructure/Worker",
            "Register a module-owned task run store such as Gma.Modules.TaskRuntime.Persistence before starting task workers."));
        builder.Services
            .AddOptions<TaskWorkerOptions>()
            .Bind(builder.Configuration.GetSection(TaskWorkerOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TaskWorkerOptions>, TaskWorkerOptionsValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TaskWorkerService>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TaskTimeoutScannerService>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TaskMetricsSamplerService>());

        return builder;
    }

    public static IHostApplicationBuilder AddTaskRunScheduling(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddTaskInfrastructure();
        builder.ProvideFeature(TasksCompositionFeatures.SchedulerProvided("Gma.Framework.Tasks.Infrastructure"));
        builder.RequireFeature(TasksCompositionFeatures.RunStoreRequired(
            "Gma.Framework.Tasks.Infrastructure/Scheduler",
            "Register a module-owned task run store such as Gma.Modules.TaskRuntime.Persistence before starting task scheduling."));
        builder.Services
            .AddOptions<TaskRunSchedulerOptions>()
            .Bind(builder.Configuration.GetSection(TaskRunSchedulerOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TaskRunSchedulerOptions>, TaskRunSchedulerOptionsValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TaskRunSchedulerService>());

        return builder;
    }

    private sealed class TaskInfrastructureRegistrationMarker;
}

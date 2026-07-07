namespace Gma.Framework.Tasks;

using Gma.Framework.ModuleComposition;

public static class TasksCompositionFeatures
{
    public static readonly CompositionFeatureId Infrastructure = new("tasks.infrastructure");
    public static readonly CompositionFeatureId RunStore = new("tasks.run-store");
    public static readonly CompositionFeatureId RuntimeReporter = new("tasks.runtime-reporter");
    public static readonly CompositionFeatureId ControlChannel = new("tasks.control-channel");
    public static readonly CompositionFeatureId CqrsDispatcher = new("tasks.cqrs-dispatcher");
    public static readonly CompositionFeatureId TenantScope = new("tasks.tenant-scope");
    public static readonly CompositionFeatureId Worker = new("tasks.worker");
    public static readonly CompositionFeatureId Scheduler = new("tasks.scheduler");

    public static ProvidedCompositionFeature InfrastructureProvided(string provider) =>
        new(Infrastructure, provider, "Task runtime infrastructure services are registered.");

    public static ProvidedCompositionFeature RunStoreProvided(string provider) =>
        new(RunStore, provider, "Persisted task run store is registered.");

    public static ProvidedCompositionFeature RuntimeReporterProvided(string provider) =>
        new(RuntimeReporter, provider, "Task runtime progress reporter is registered.");

    public static ProvidedCompositionFeature ControlChannelProvided(string provider) =>
        new(ControlChannel, provider, "Task control channel is registered.");

    public static ProvidedCompositionFeature CqrsDispatcherProvided(string provider) =>
        new(CqrsDispatcher, provider, "Task handlers can dispatch CQRS commands through the task bridge.");

    public static ProvidedCompositionFeature TenantScopeProvided(string provider) =>
        new(TenantScope, provider, "Tenant-scoped task handlers resolve tenant context before execution.");

    public static ProvidedCompositionFeature WorkerProvided(string provider) =>
        new(Worker, provider, "Task worker hosted services are registered.");

    public static ProvidedCompositionFeature SchedulerProvided(string provider) =>
        new(Scheduler, provider, "Task run scheduler hosted service is registered.");

    public static RequiredCompositionFeature InfrastructureRequired(string owner, string? reason = null, bool optional = false) =>
        new(Infrastructure, owner, optional, reason);

    public static RequiredCompositionFeature RunStoreRequired(string owner, string? reason = null, bool optional = false) =>
        new(RunStore, owner, optional, reason);

    public static RequiredCompositionFeature RuntimeReporterRequired(string owner, string? reason = null, bool optional = false) =>
        new(RuntimeReporter, owner, optional, reason);

    public static RequiredCompositionFeature ControlChannelRequired(string owner, string? reason = null, bool optional = false) =>
        new(ControlChannel, owner, optional, reason);

    public static RequiredCompositionFeature CqrsDispatcherRequired(string owner, string? reason = null, bool optional = false) =>
        new(CqrsDispatcher, owner, optional, reason);

    public static RequiredCompositionFeature TenantScopeRequired(string owner, string? reason = null, bool optional = false) =>
        new(TenantScope, owner, optional, reason);

    public static RequiredCompositionFeature WorkerRequired(string owner, string? reason = null, bool optional = false) =>
        new(Worker, owner, optional, reason);

    public static RequiredCompositionFeature SchedulerRequired(string owner, string? reason = null, bool optional = false) =>
        new(Scheduler, owner, optional, reason);
}

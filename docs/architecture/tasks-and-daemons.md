# Tasks and Daemons

Tasks and daemons are optional. The shared core defines contracts, module metadata, scheduler-neutral runtime state shapes, and a hosted worker loop. No default host starts a task runner; a host must explicitly compose the task runtime store, worker runtime, and task-owning modules.

## Model

A module owns its task payloads the same way it owns commands, queries, domain events, and persistence.

- A task is a finite run, usually queued or triggered by an operator, API, schedule, or integration event.
- A recurring task is a finite task created repeatedly by a schedule.
- A daemon is long-running work that keeps running until canceled or stopped.

Modules declare owned work by putting task identity on the serialized payload contract and referencing that payload from module metadata:

```csharp
[TaskName(RebuildSearchPayload.TaskName)]
[TaskPayloadVersion(RebuildSearchPayload.PayloadVersion)]
[TaskDescription("Rebuild catalog search projection.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup("search-workers")]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildSearchPayload(bool Force) : ITaskPayload
{
    public const string TaskName = "rebuild-search";
    public const int PayloadVersion = 1;
}

public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
    .Create(Name)
    .WithTask<RebuildSearchPayload>()
    .Build();
```

`TaskNameAttribute`, `TaskPayloadVersionAttribute`, `TaskDescriptionAttribute`, `TaskKindAttribute`, `TaskWorkerGroupAttribute`, `SupportsTaskControlAttribute`, `ModuleTaskDescriptor`, `WithTask<TPayload>()`, `WithTask(...)`, and `WithTasks(...)` live in `Gma.Framework.Tasks`. Scope behavior comes from `[ScopeAware]` in `Gma.Framework.Scoping`, not from the task package. The descriptor is discoverability and policy metadata. It is not runtime module discovery and does not register a worker.
Task handler identity is `(module, task, payload version)` so modules can keep old payload handlers alive while introducing a new payload shape. Worker group, scope behavior, kind, and control-message support are routing and policy metadata that must still match the module descriptor.

## Payload Contracts

Serialized task payloads implement `ITaskPayload`. If the task is visible in module metadata or can be enqueued outside the current application assembly, keep the payload type in `<Module>.Contracts`, expose task identity constants on the payload contract, and annotate it with the small task-owned attributes plus `[ScopeAware]` when an active scope is required.

Task handler code implements `ITaskHandler<TPayload>`:

```csharp
internal sealed class RebuildSearchTask : ITaskHandler<RebuildSearchPayload>
{
    public async Task HandleAsync(
        RebuildSearchPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        // use repositories, ports, cache invalidation, or commands through application contracts
    }
}
```

`TaskExecutionContext` carries run id, module name, task name, worker group, worker id, node id, attempt, optional scope id, correlation id, and whether the run was reclaimed for cancellation. Runtime adapters should pass this context into logging scopes, metrics, audit records, and command dispatch.

Keep payload handlers and their explicit registration extension in the owning module application project. Modules shared by API, admin, and worker hosts should separate normal application services from executable task handlers:

```csharp
public static IServiceCollection AddCatalogTaskHandlers(this IServiceCollection services)
{
    services.AddTaskHandler<GenerateReportTaskPayload, GenerateReportTaskHandler>(CatalogModuleMetadata.Name);
    return services;
}
```

The execution host calls `AddCatalogTaskHandlers()` only when it is configured to run Catalog tasks. A module used exclusively by a worker may keep task handler registration in its single application extension. `AddTaskHandler<TPayload,THandler>(moduleName)` reads task-owned attributes and generic metadata contributors from the payload type. The older explicit overload remains available for unusual cases, but the attribute-backed overload is the default for module-owned tasks. Architecture tests compare registered handlers, payload attributes, and module descriptors so task docs, module metadata, and runtime registration drift together.

Projection rebuilds are a task use case, not a separate scheduler. Use `Gma.Framework.ProjectionRebuild` for the task-neutral batch/checkpoint loop and `Gma.Framework.ProjectionRebuild.Tasks` when a task handler needs to adapt rebuild progress/control to task runtime services. Keep the source contract, writer, cursor semantics, and checkpoint persistence in the producer/consumer modules that own the data. See [Projection Rebuild Tasks](projection-rebuild-tasks.md).

## Communication

Runner-to-system communication uses `ITaskRuntimeReporter`:

- heartbeat reporting;
- bounded progress reporting through `TaskProgress`.

System-to-runner communication uses `ITaskControlChannel`, `ITaskControlLoop`, and `TaskControlMessage`:

- cancellation or pause requests;
- operator commands;
- daemon-specific commands.

`TaskControlCommandNames` defines the standard control commands `tasks.cancel`, `tasks.drain`, `tasks.pause`, and `tasks.resume`. `ITaskControlLoop` is a small helper over the lower-level channel: it polls pending messages, returns a `TaskControlPollResult`, and lets payload code mark messages handled or failed after the handler has actually acted on them. `TaskControlLoopExtensions` adds reusable cooperative behavior for cancel/drain and pause-until-resume loops.

Task payload code that needs to call application behavior should use `ITaskCommandDispatcher` from `Gma.Framework.Tasks.Cqrs` or normal CQRS contracts. Hosts compose `AddTaskCqrs()` only when at least one registered task handler needs command dispatch. This keeps payloads independent from HTTP, CLI, scheduler APIs, and module internals.

## Runtime Store Contract

The shared task store contract is intentionally not tied to EF, Quartz.NET, Hangfire, NATS, or hosted services.

`ITaskRunStore` combines enqueueing, lease-based claiming, status reporting, and control messages:

- `TaskRunRequest` represents an enqueue request with module/task identity, worker group, payload JSON, optional scope id, correlation id, schedule time, requester, and max attempts.
- `TaskRunRequest.PayloadVersion` selects the matching handler registration.
- `TaskRunRequest.DeduplicationKey` lets producers make active queued/running/retry work idempotent without exposing physical provider keys.
- `TaskWorkerClaim` represents a worker's claim request, including worker group, worker id, node id, batch size, and lease duration.
- `TaskRunLease` is the immutable run lease handed to a worker, including cancellation intent, and can create the `TaskExecutionContext` passed into payload code.
- `TaskRunStatusTransitions` centralizes claim/start/complete/cancel rules so adapters do not invent incompatible state machines.
- `TaskRunStatusNames` centralizes external status names. API, CLI, docs, and metrics use kebab-case names such as `retry-scheduled` and `cancellation-requested`; enum-style names are accepted only as compatibility input.
- `TaskRunStats` and `TaskRunStatsFilter` provide a small operational read model for status counts without coupling operators to EF.

Cancellation is best-effort and lease-aware:

- queued or retry-scheduled runs are marked `Canceled` immediately;
- leased or running runs move to `CancellationRequested`;
- if a cancellation-requested worker disappears, the expired lease can be reclaimed and the replacement worker marks the run `Canceled` without invoking the payload handler.

The worker automatically heartbeats every running handler from an independent service scope. `Tasks:Worker:HeartbeatInterval` may override the default one-third-of-lease interval, but must remain positive and shorter than `LeaseDuration`. Handler progress reports also renew the owned lease and add operator-visible state; progress reporting is not required for lease safety. If automatic heartbeat persistence fails, the worker cancels that handler because it can no longer prove exclusive lease ownership. Manually-created execution contexts do not renew leases unless they opt into a positive lease-extension window.

Concrete runtimes persist these concepts in an optional runtime module or adapter. Module application code should not persist task rows directly.

## EF Runtime Module

`Gma.Modules.TaskRuntime.Persistence` owns the `tasks` schema and maps:

- `task_runs`
- `task_control_messages`

Provider-specific migrations exist for SQL Server and PostgreSQL. The store uses serializable transactions for claim batches and marks runs only when the current worker owns the lease. Runtime store methods are self-committing because workers, scanners, and schedulers usually run outside a CQRS request; the optional `TaskRuntimeUnitOfWork` skips its commit when the store already saved the changes.

Compose it explicitly:

```csharp
builder.AddGmaInfrastructure();
builder.AddTaskInfrastructure();
builder.AddTaskRuntimePersistence();
builder.AddTaskCqrs(); // only when registered task handlers use ITaskCommandDispatcher
builder.AddTaskWorkerRuntime();
builder.Services.AddTaskSamplesApplication();
builder.Services.AddTaskSamplesTaskHandlers();
```

`Host.Worker` is the default production process for these loops when HTTP isolation matters. It still keeps task execution opt-in:

```text
Worker__Modules__TaskRuntime=true
Tasks__Worker__Enabled=true
Tasks__Worker__WorkerGroups__0=projection-workers
```

Then compose the task-owning module group explicitly, such as `Worker__Modules__Ordering=true` for the compiled Catalog-to-Ordering projection rebuild task or `Worker__Modules__TaskSamples=true` for the sample handlers. If `Tasks:Worker:Enabled=true` but no persisted run store is composed, module composition validation fails before the worker starts.

Worker configuration lives under `Tasks:Worker`:

- `Enabled`
- `WorkerGroups`
- `BatchSize`
- `PollInterval`
- `LeaseDuration`
- `HandlerTimeout`
- `RetryBaseDelay`
- `RetryMaxDelay`
- optional `WorkerId` and `NodeId`
- `MetricsSamplerEnabled`
- `MetricsSamplerPollInterval`

The hosted worker:

- claims due runs by worker group;
- logs transient claim/processing infrastructure failures and retries instead of exiting permanently;
- sets tenant context for tenant-scoped handlers;
- deserializes the registered payload type;
- invokes `ITaskHandler<TPayload>`;
- renews the lease through heartbeat/progress reports;
- marks success only after handler completion;
- marks `TaskRunCanceledException` as terminal `Canceled`;
- marks failure with retry scheduling on handler errors or timeouts;
- marks cancellation-requested reclaimed leases as canceled without requiring the handler to still be registered;
- leaves the lease to expire on host shutdown cancellation.
- processes leases with bounded per-worker-host concurrency;
- emits bounded `{ApplicationIdentity:Namespace}.tasks` metrics for claimed, completed, duration, timed-out, queue-depth, and active-run measurements;
- runs an optional stale timeout scanner that marks abandoned leases/runs as `TimedOut`.

The metrics sampler reads `ITaskRunStore.GetStatsAsync(...)` and updates observable gauges:

- `{ApplicationIdentity:Namespace}.tasks.queue.depth` for `Queued` and `RetryScheduled` runs;
- `{ApplicationIdentity:Namespace}.tasks.active.runs` for `Leased`, `Running`, and `CancellationRequested` runs.

Gauge tags are bounded to task status and do not include tenant ids, payloads, run ids, or control-message ids.

The scheduler, timeout scanner, metrics sampler, and worker loop are long-running infrastructure services. Transient store, provider, or schedule-provider failures are logged and retried on the next poll. Host shutdown cancellation still stops the services cleanly.

## Optional Scheduling

`AddTaskRunScheduling()` starts a small hosted adapter that reads code-defined `ITaskScheduleProvider` schedules and enqueues `TaskRunRequest`s. It is disabled by default through `Tasks:Scheduler:Enabled=false`.

The adapter is intentionally scheduler-neutral:

- schedules are declared in module application code through `ScheduledTaskDefinition`;
- it writes only to `ITaskRunStore`;
- deterministic per-interval dedupe keys are generated as `schedule:<module>:<task>:<schedule>:v<payload-version>:<occurrence>`;
- no domain/application code depends on Quartz.NET, Hangfire, or hosted-service APIs.

External schedulers can still be added later as explicit adapters that create the same `TaskRunRequest` shape.

## Optional Admin

`Gma.Modules.TaskRuntime.AdminCli` and `Gma.Modules.TaskRuntime.AdminApi` are optional front doors. They are not registered in `Host.Api`, `Host.AdminCli`, or `Host.AdminApi` by default.

CLI commands:

- `tasks runs list`
- `tasks runs list --status retry-scheduled`
- `tasks runs stats`
- `tasks runs get --run-id <id>`
- `tasks runs enqueue --module <module> --task <task> --payload-json <json>|--payload-file <path>`
- `tasks runs control --run-id <id> --command tasks.pause|tasks.resume|tasks.cancel|tasks.drain --yes`
- `tasks runs cancel --run-id <id> --yes`
- `tasks runs retry --run-id <id> --yes`

Admin API routes live under `/api/admin/tasks/runs` and use the same application commands/queries as the CLI. Permissions are declared as `tasks.runs.read`, `tasks.runs.create`, `tasks.runs.control`, `tasks.runs.cancel`, and `tasks.runs.retry`.

## Remaining Runtime Work

Future optional slices can add:

- live status streaming;
- provider-level stress testing for higher worker counts;
- Quartz.NET or Hangfire adapters if a project needs their clustering, dashboards, calendars, or advanced scheduling semantics.

The default remains small: persistent tasks, hosted workers, code-defined schedules, and admin controls are all opt-in composition.

## Guardrails

- Default hosts do not start task workers.
- Domain projects do not reference scheduler, hosting, EF, HTTP, or admin APIs.
- Application payloads depend on `Gma.Framework.Tasks`, `Gma.Framework.Tasks.Cqrs` when they dispatch application commands, `Gma.Framework.ProjectionRebuild.Tasks` when they adapt rebuilds to task progress/control, CQRS contracts, and module ports.
- External scheduler packages stay in explicit adapter projects.
- Store implementations use `ITaskRunStore` and `TaskRunStatusTransitions` instead of ad hoc status changes.
- Task worker hosts call `AddTaskWorkerRuntime()` explicitly and must also compose a concrete `ITaskRunStore`.
- `Host.Worker` calls `AddTaskWorkerRuntime()` only when `Tasks:Worker:Enabled=true`; disabled task settings must not register worker hosted services.
- `Gma.Framework.Tasks.Infrastructure` is tenant-neutral. Worker hosts that execute payloads marked with `[ScopeAware]` in a tenant-aware app must also compose `AddTenantTaskExecutionContext()` from `Gma.Framework.Tenancy.Tasks`.
- Task scheduler hosts call `AddTaskRunScheduling()` explicitly and must also compose a concrete `ITaskRunStore`.
- Running a task on another node must still use module contracts, integration events, or control messages, not direct cross-module internals.

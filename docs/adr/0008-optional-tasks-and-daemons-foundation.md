# ADR 0008: Optional Tasks and Daemons Foundation

## Status

Accepted.

## Context

The skeleton needs a production-ready direction for background work without turning every application into a job-processing platform by default.

The backlog calls for:

- task and daemon execution;
- future multi-node placement;
- monitoring and control;
- bidirectional communication between the system and running work;
- a pleasant way for task payload code to use application commands.

.NET hosted services are the natural runtime primitive for in-process workers. Quartz.NET and Hangfire are mature choices for scheduling, persistence, dashboards, clustering, retries, and distributed execution. The skeleton should not hide those systems behind domain code, but it also should not force them into every project.

## Decision

Add a small `Gma.Framework.Tasks` contract surface and task metadata support through module descriptor feature extensions.

The first foundation includes:

- `ITaskPayload` and `ITaskHandler<TPayload>` for module-owned task payload code;
- `TaskExecutionContext` for run id, module, task name, worker group, worker id, node id, attempt, tenant, correlation, and cancellation intent;
- `TaskProgress` for bounded progress reporting;
- `TaskControlMessage`, `ITaskControlChannel`, `ITaskControlLoop`, `TaskControlCommandNames`, and `TaskControlLoopExtensions` for system-to-runner control messages;
- `ITaskRuntimeReporter` for runner-to-system heartbeat/progress reporting;
- `ITaskCommandDispatcher` in `Gma.Framework.Tasks.Cqrs` for dispatching normal CQRS commands from task payload code with task-run context; hosts compose `AddTaskCqrs()` only when registered task handlers need it;
- `TaskRunRequest`, `TaskWorkerClaim`, `TaskRunLease`, `ITaskRunStore`, `TaskRunStats`, `TaskRunStatusTransitions`, and `TaskRunStatusNames` for scheduler-neutral run persistence, requester provenance, leasing, stats, cancellation, status rules, and stable status wire names;
- payload versioning and active-run deduplication on `TaskRunRequest`;
- `TaskHandlerRegistration`, `ITaskHandlerRegistry`, and explicit attribute-backed `AddTaskHandler<TPayload,THandler>(moduleName)` registration;
- `ITaskScheduleProvider`, `ScheduledTaskDefinition`, and `AddTaskRunScheduling()` for optional code-defined schedules that enqueue task requests only;
- `TaskRunStatus` and `TaskControlMessageStatus` enums;
- split task attributes, `ModuleTaskDescriptor`, `ModuleTaskKind`, `WithTask<TPayload>()`, `WithTask(...)`, and `WithTasks(...)` so modules can declare owned tasks and daemons without adding task-specific properties to the root module descriptor.

The first runtime implementation adds:

- `Gma.Modules.TaskRuntime.Persistence`, an optional EF-backed runtime module with SQL Server and PostgreSQL migrations in the `tasks` schema;
- `AddTaskWorkerRuntime()`, an explicit hosted-worker composition hook;
- bounded worker concurrency, task counters/duration, queue-depth and active-run gauges, retrying hosted loops for transient runtime failures, and a stale timeout scanner;
- optional `Gma.Modules.TaskRuntime.AdminCli` and `Gma.Modules.TaskRuntime.AdminApi` front doors for listing, inspecting, status-count stats, enqueueing, control messages, canceling, and retrying runs;
- `TaskSamples`, a compiled optional example module that declares and registers tenant-scoped sample tasks.

No external scheduler package or default host worker registration is added in this slice.

## Consequences

Task payload code can depend on shared application contracts, not on a concrete scheduler or transport. Runtime adapters can be:

- a local `BackgroundService` runner for simple apps;
- the persistent EF-backed `TaskRuntime` module;
- a Quartz.NET adapter for advanced schedules and clustering;
- a Hangfire adapter for job queues and dashboard-driven operations;
- a NATS-backed remote worker adapter for separate worker nodes.

Tasks are executable when a host explicitly composes the runtime store, worker runtime, and task-owning application modules. The tradeoff is that dashboard-grade scheduling, live streaming, and external scheduler adapters are still future optional adapters rather than default dependencies.

## Guardrails

- Domain and application modules should not reference scheduler packages directly.
- Modules declare task metadata in public contracts, but hosts still opt into task runtime composition explicitly.
- Task control messages are commands at the contract boundary; payload code should poll them through `ITaskControlLoop` or `TaskControlLoopExtensions` and call application commands through `ITaskCommandDispatcher` from `Gma.Framework.Tasks.Cqrs` or `IRequestDispatcher`, not through HTTP or module internals. CQRS dispatch is an explicit bridge, not part of baseline task worker infrastructure.
- Task store implementations should use `TaskRunStatusTransitions` for lease, retry, and terminal-state rules.
- Cancellation is persisted and lease-aware: unstarted work can terminate immediately, heartbeat/progress renews owned leases, and abandoned cancellation-requested leases can be reclaimed and marked canceled without running payload code.
- Default hosts must not call `AddTaskWorkerRuntime()` or register task example modules.
- Default hosts must not call `AddTaskRunScheduling()` or register task admin modules unless intentionally composed.
- Multi-node execution must use leases, heartbeats, and idempotent payload behavior before it is considered production-ready.
- Long-running daemon loops must support cancellation, heartbeat, progress, and control-message polling.

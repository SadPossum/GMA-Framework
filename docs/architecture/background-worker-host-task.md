# Background Worker Host Task

This is an implementation task brief for a future production hardening slice. It should be handed to an agent together with the current repository checkout.

## Summary

Add an optional `Host.Worker` composition root for background infrastructure.

The worker should let production deployments keep HTTP hosts focused on request/response while a separate process drains outboxes, publishes to NATS, runs NATS consumers, updates module-owned projections, and executes task runtime work.

This does not turn the skeleton into microservices. It is still one modular monolith codebase with explicit module composition. The worker is just a separate host process with a background runtime profile.

## Task Text

Implement an optional `Host.Worker` project that can run background processing separately from `Host.Api` and `Host.AdminApi`.

The first implementation should support these production profiles:

- API-only/simple profile: HTTP hosts can keep the current behavior for small deployments, including in-process outbox publishing when `NatsJetStream:Enabled=true`.
- Separated production profile: HTTP hosts write business data and outbox rows only; `Host.Worker` owns outbox publishing, NATS consumers, inbox processing, task workers, and projection rebuild execution.
- Task-only profile: `Host.Worker` can run `TaskRuntime` worker loops without requiring NATS consumers.
- Messaging-only profile: `Host.Worker` can drain module outboxes and/or consume NATS events without enabling task workers.

Keep the feature explicitly composed. Do not add assembly-wide worker discovery, automatic module scanning, or endpoint-based magic.

## Current Context

The repo already has most of the runtime pieces:

- `Host.Api` and `Host.AdminApi` call `AddMessagingInfrastructure()` and `AddConfiguredNatsJetStreamMessaging()`.
- `AddConfiguredNatsJetStreamMessaging()` is a no-op unless `NatsJetStream:Enabled=true`.
- When NATS publishing is enabled, `AddNatsJetStreamMessaging()` replaces `IEventBus` with the NATS implementation and registers `OutboxPublisherService`.
- `OutboxPublisherService` claims pending rows from every registered module `IOutboxStore` and publishes through `IEventBus`.
- NATS consumers are separate. They start only when a host calls `AddNatsJetStreamConsumers()` and `NatsConsumers:Enabled=true`.
- Task processing is separate. Hosts must explicitly compose `Gma.Modules.TaskRuntime.Persistence`, task handler modules, and `AddTaskWorkerRuntime()`.

The current design is safe for small systems because API requests do not publish to NATS inline. Writes add outbox rows inside the module database transaction, and background publishing happens later.

The production concern is resource isolation: if the API process also runs outbox publishers, consumers, and task workers, background work can compete with request traffic for CPU, memory, thread pool time, EF contexts, database connections, and broker connections.

## Decision

Add `Host.Worker` as an optional process-level boundary for background work.

Use it to separate runtime pressure, not domain ownership:

```text
Host.Api
  -> HTTP request/response
  -> command/query dispatch
  -> business data writes
  -> outbox row writes
  -> returns to caller

Host.Worker
  -> claims outbox rows
  -> publishes integration events to NATS
  -> consumes NATS events
  -> updates inboxes and projections
  -> runs TaskRuntime workers and projection rebuild tasks
```

The worker must keep the same modular rules as every other host:

- modules are explicitly composed;
- modules own their own persistence;
- consumers update local projections through module-owned repositories;
- no module reads another module's EF internals;
- NATS is an integration-event transport, not a synchronous query bus.

## Non-Goals

- Do not replace `TaskRuntime`; the worker hosts it.
- Do not replace module outboxes, inboxes, or NATS consumers.
- Do not move business rules into `Host.Worker`.
- Do not introduce synchronous request/reply over NATS for cross-module reads.
- Do not create cross-module foreign keys or direct cross-module EF references.
- Do not make the worker mandatory for small projects.
- Do not auto-register every module or every task handler by assembly scanning.
- Do not map public or admin business endpoints from `Host.Worker`.
- Do not require Kubernetes, a cloud scheduler, or an external job framework in the first slice.

## Proposed Project Shape

Add:

```text
src/Hosts/Host.Worker/
  Host.Worker.csproj
  Program.cs
  appsettings.json
  appsettings.Development.json
```

The project should use the same shared host conventions as the API hosts where they make sense:

- configured Serilog;
- service defaults and OpenTelemetry;
- shared infrastructure;
- provider-selected persistence options;
- messaging infrastructure;
- optional NATS publishing;
- optional NATS consumers;
- optional task infrastructure and task worker runtime;
- no HTTP endpoint mapping except health/metrics behavior already provided by service defaults, if the chosen host model exposes those.

## Composition Model

Keep worker composition explicit.

The first slice may compose module services directly in `Program.cs`:

```csharp
builder.AddGmaInfrastructure();
builder.AddMessagingInfrastructure();
builder.AddConfiguredNatsJetStreamMessaging();
builder.AddNatsJetStreamConsumers();

builder.Services.AddCatalogApplication();
builder.AddCatalogPersistence();

builder.Services.AddOrderingApplication();
builder.Services.AddOrderingTaskHandlers();
builder.AddOrderingPersistence();

builder.Services.AddTaskRuntimeApplication();
builder.AddTaskRuntimePersistence();
builder.AddTaskWorkerRuntime();
```

If direct composition becomes noisy, add a small worker-surface abstraction later, for example:

```csharp
public interface IWorkerModule
{
    string Name { get; }

    void RegisterWorkerServices(IHostApplicationBuilder builder);
}
```

Then compose it explicitly:

```csharp
builder.AddWorkerModule<CatalogWorkerModule>();
builder.AddWorkerModule<OrderingWorkerModule>();
builder.AddWorkerModule<TaskRuntimeWorkerModule>();
```

Do not add reflection-based discovery. `IWorkerModule` should be only a convenience surface like public API/admin API/admin CLI module registration.

## Outbox Publishing Requirements

The worker can only publish outbox rows for modules whose outbox stores are registered in that process.

Requirements:

- A worker that drains Auth outbox rows must compose Auth persistence.
- A worker that drains Catalog outbox rows must compose Catalog persistence.
- A worker that does not compose a module must not attempt to drain that module's outbox.
- Multiple workers may run at the same time; claiming must remain lock-based and idempotent.
- Workers must honor existing outbox options: batch size, poll interval, lock duration, and max attempts.
- Exhausted messages must remain inspectable and must not be silently deleted.
- Publishing must continue to treat duplicate broker acknowledgements as successful idempotent publishes.
- API hosts must be able to run with `NatsJetStream:Enabled=false` while still writing outbox rows through module repositories.

Production guidance:

```text
API host:
  AddMessagingInfrastructure()
  NatsJetStream:Enabled=false
  NatsConsumers:Enabled=false

Worker host:
  AddMessagingInfrastructure()
  AddConfiguredNatsJetStreamMessaging()
  NatsJetStream:Enabled=true
  drains registered module outboxes
```

## Consumer Requirements

NATS consumers should run only in hosts that explicitly register them.

Requirements:

- `Host.Worker` must call `AddNatsJetStreamConsumers()` only when the deployment intends to consume backend events.
- `NatsConsumers:Enabled=false` remains the default.
- A consumer-enabled worker must also have a configured NATS connection.
- Each subscription still requires a module-owned `IInboxStore`.
- A consuming module must own idempotency and projection writes.
- Consumer handlers must continue to run through the shared inbox store so duplicate delivery is safe.
- Tenant-scoped events must set tenant context before handler invocation.
- Poison messages with invalid metadata or invalid payloads must terminate or retry according to existing consumer rules.

Do not introduce wildcard module subscriptions in the first slice. Producer-to-consumer bindings stay explicit.

## TaskRuntime Requirements

The worker should be the natural place to compose task runtime loops.

Requirements:

- `Host.Worker` may call `AddTaskWorkerRuntime()` when `Tasks:Worker:Enabled=true`.
- The worker must also compose a concrete `ITaskRunStore`, normally through `AddTaskRuntimePersistence()`.
- The worker must compose only task-owning modules that should run in that deployment.
- Task handler registration remains explicit and must continue to match module metadata.
- Worker groups must be deployable separately, for example `projection-workers`, `export-workers`, or the default group.
- Long-running tasks must use existing heartbeat/progress and control-message mechanisms.
- Projection rebuild tasks continue to use consumer-owned checkpoints and module-owned writers.

`Host.Worker` should not add a second scheduler or a second task status model.

## Configuration Requirements

Add worker-specific appsettings that are safe by default.

Suggested defaults:

```json
{
  "NatsJetStream": {
    "Enabled": false
  },
  "NatsConsumers": {
    "Enabled": false
  },
  "Tasks": {
    "Worker": {
      "Enabled": false
    }
  }
}
```

Production deployment can then enable only the required role:

```text
NatsJetStream__Enabled=true
NatsConsumers__Enabled=true
Tasks__Worker__Enabled=true
Tasks__Worker__WorkerGroups__0=projection-workers
```

Consider adding an explicit worker role setting if the first implementation needs separate startup validation:

```text
Worker__Role=messaging
Worker__Role=tasks
Worker__Role=all
```

Do not add this setting unless it drives real validation or clearer operational logs.

## AppHost Requirements

Update `src/Hosts/AppHost` so local development can optionally run the worker.

Requirements:

- Keep current local API behavior working.
- Add a configuration flag such as `AppHost:Worker:Enabled`.
- When enabled, compose `Host.Worker` with SQL Server, PostgreSQL, NATS, and any other configured infrastructure it needs.
- Prefer keeping the worker disabled by default until the local workflow is documented.
- If worker is enabled, decide whether API NATS publishing should also stay enabled for compatibility or be disabled to demonstrate separated publishing.
- Document the selected local profile.

Avoid a local setup where both API and worker unexpectedly drain the same outbox unless that is an intentional multi-instance publisher test.

## Observability Requirements

Worker logs and metrics must make background behavior visible without leaking sensitive data.

Requirements:

- Include host role, module name, worker id, node id, worker group, message subject, task name, and run id where applicable.
- Do not tag metrics with tenant ids, user ids, resource ids, or payload values.
- Surface outbox backlog, published count, failed count, and publish duration.
- Surface inbox processed/duplicate/failed counts and handler duration.
- Surface task queue depth, active runs, stale leases, retries, control messages, and handler duration.
- Add startup logs that state which runtime loops are enabled or disabled.
- Make disabled loops quiet and intentional, not warning noise.

Health behavior should distinguish:

- process is alive;
- configured dependencies are reachable;
- worker loops are running;
- backlog is growing or stuck.

Backlog growth should be an alerting signal, not necessarily a liveness failure.

## Shutdown And Retry Requirements

The worker must behave cleanly during deploys and restarts.

Requirements:

- Stop claiming new work after cancellation starts.
- Let in-flight publish/consume/task operations finish within host shutdown limits when possible.
- Preserve idempotency if shutdown happens after broker publish but before local processed marking.
- Let outbox locks expire and be reclaimed by another worker.
- Let NATS messages redeliver when they are not acknowledged.
- Let task leases expire or be renewed through heartbeat/progress according to existing TaskRuntime rules.
- Do not mark work successful unless the durable side effect and local state transition succeeded.

## Deployment Requirements

Deployment docs should describe at least two supported modes.

Simple mode:

```text
Host.Api replicas run HTTP and outbox publishing.
Host.Worker is not deployed.
```

Separated production mode:

```text
Host.Api replicas run HTTP only.
Host.Worker replicas drain outboxes, consume NATS events, and run tasks.
```

Operational notes:

- Run module migrations before starting worker replicas.
- Size worker database connection pools separately from API pools.
- Scale API and worker replicas independently.
- Tune outbox batch size and poll interval for backlog and database pressure.
- Tune NATS consumer batch size, ack wait, max deliver, handler timeout, and nak delay for handler behavior.
- Keep at least one worker replica running before disabling API-side outbox publishing.
- Have an explicit rollback path: re-enable API-side outbox publishing or roll forward with worker replicas.

## Testing Requirements

Add focused tests rather than broad end-to-end coverage only.

Architecture tests:

- Default `Host.Api` must not register NATS consumers.
- Default `Host.AdminApi` must not register NATS consumers.
- `Host.Worker` must not map public or admin business endpoints.
- Domain and application projects must not reference NATS client packages.
- Module worker composition must remain explicit.
- Task handler metadata must still match module descriptors.

Host startup tests:

- Worker starts with all background loops disabled.
- Worker fails fast when NATS publishing is enabled without a NATS connection.
- Worker fails fast when consumers are enabled without required inbox stores.
- Worker starts task runtime only when a concrete task store is composed.

Integration tests:

- API with publishing disabled can commit a command and write an outbox row.
- Worker with publishing enabled drains that outbox row and marks it processed.
- A second worker instance does not duplicate local processing beyond existing idempotency guarantees.
- Worker consumer updates a module-owned projection through inbox processing.
- Worker task runtime claims and completes a real task handler.
- Worker shutdown leaves in-flight outbox, inbox, and task work retryable.

Use Docker-backed tests only where a real broker or provider behavior matters. Keep unit tests for composition validation and option guards.

## Documentation Requirements

Update:

- `docs/README.md`;
- `src/Framework/docs/architecture/messaging-and-outbox.md`;
- `src/Framework/docs/architecture/messaging-consumers.md`;
- `src/Framework/docs/architecture/tasks-and-daemons.md`;
- `src/Framework/docs/guidelines/deployment-guidelines.md`;
- `docs/getting-started/setup.md`;
- `src/Framework/docs/architecture/production-readiness-backlog.md`.

Docs must clearly explain:

- when a project can skip `Host.Worker`;
- how to enable separated publishing;
- which modules the worker must compose;
- how to run consumers;
- how to run task workers;
- how to avoid accidentally running duplicate local background loops.

## Acceptance Criteria

- `Host.Worker` exists and builds.
- The solution includes `Host.Worker`.
- Worker appsettings are safe by default.
- API hosts can run without NATS publishing and still write outbox rows.
- Worker can publish registered module outboxes to NATS.
- Worker can run NATS consumers when explicitly enabled.
- Worker can run TaskRuntime workers when explicitly enabled.
- AppHost can optionally start the worker for local development.
- Tests prove disabled defaults, explicit composition, outbox publishing, consumer processing, and task execution.
- Deployment docs describe simple mode and separated production mode.
- Production readiness backlog links this task and records the remaining follow-up items.

## Implementation Slices

1. Add this task doc and backlog entry.
2. Add `Host.Worker` project with safe defaults and solution registration.
3. Compose shared infrastructure, messaging infrastructure, and optional NATS publishing.
4. Add optional NATS consumer composition and startup validation.
5. Add optional TaskRuntime worker composition.
6. Add AppHost optional worker profile.
7. Add host startup and architecture tests.
8. Add one Docker-backed integration test that proves separated API write -> worker publish -> consumer projection flow.
9. Update deployment and setup docs.
10. Tune defaults only after test evidence or a real deployment profile proves the need.

## Open Questions For Implementation

- Should the first slice use direct `Program.cs` composition only, or add `IWorkerModule` immediately for symmetry with API/admin module surfaces?
- Should AppHost default to simple mode for ease of local development or separated mode to exercise production shape?
- Should API-side publishing remain available forever as simple mode, or eventually become an explicit compatibility profile?
- Which compiled module should be the first worker integration proof: Catalog to Ordering projection, Notifications durable request ingestion, or projection rebuild through TaskRuntime?
- Do we need a tiny operational read model for outbox backlog by module, or are metrics enough for the first worker slice?

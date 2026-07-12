# Durable Runtime Hardening Task

Status: active implementation plan

## Summary

Harden GMA's durable messaging and task execution runtimes for reuse by production modular monoliths.

This work must remain product-neutral. GMA owns generic delivery, leasing, retention mechanics, transport configuration, observability, and conformance tests. Applications own business payloads, data classification, legal holds, recovery objectives, retention periods, deployment sizing, and domain-specific scheduling.

BunkFy is the first production acceptance project. StayQuest is expected to become a second adopter. Neither application's domain vocabulary or workflow may enter GMA packages.

## Decision

Complete correctness and lifecycle hardening before throughput optimization:

1. task worker lease ownership and fair scheduling;
2. outbox and inbox terminal-record lifecycle;
3. bounded and validated JetStream management;
4. TaskRuntime terminal-history lifecycle;
5. reusable failure, recovery, and scale conformance tests;
6. optional throughput improvements that preserve documented ordering semantics.

All features remain explicitly composed. Safe-disabled defaults remain valid for small applications, while production hosts must make explicit lifecycle and broker-management choices.

## Ownership Boundaries

### GMA Framework

- task claim, dispatch, heartbeat, timeout, retry, and cancellation mechanics;
- generic outbox and inbox store contracts and EF implementations;
- bounded journal cleanup mechanics and metrics;
- NATS JetStream provisioning and validation options;
- broker acknowledgement and redelivery safety;
- generic health, backlog, and runtime metrics;
- reusable provider-neutral and provider-backed conformance tests.

### Optional GMA Modules

- `Gma.Modules.TaskRuntime` owns persisted task-run and control-message lifecycle;
- optional modules own retention for their own durable records;
- modules must not depend on application-specific tenant catalogs, legal holds, or business archives.

### Skeleton And Composition Repositories

- production-safe example configuration;
- explicit host/profile wiring;
- Docker-backed integration and failure-recovery proof;
- migration/source-pointer validation;
- documentation showing both minimal and separated-worker deployments.

### Applications

- concrete retention durations and recovery windows;
- event payload minimization and sensitive-data policy;
- legal holds, evidence preservation, and archival semantics;
- worker groups, process topology, concurrency, and alert thresholds;
- domain task schedules and authoritative scope enumeration;
- application-specific performance and failure workloads.

## Slice 1: Task Worker Correctness

### Problem

The current worker claims `BatchSize` leases before applying `MaxConcurrency`. A worker configured with `BatchSize > MaxConcurrency` can leave owned leases waiting behind its semaphore. Those leases can expire before execution and be reclaimed by another worker or timeout scanner.

The current loop also processes configured worker groups serially. A slow group can prevent later groups from being polled, even when they represent operationally independent work.

Long-running handlers renew leases only when application code reports heartbeat or progress. Lease ownership should not depend on optional business progress reporting.

### Required Behavior

- Never claim more work than the worker can start within its available execution capacity.
- Do not keep a local queue of unstarted persisted leases.
- Poll configured worker groups fairly.
- Keep `MaxConcurrency` as a host-wide upper bound unless a future explicit worker-pool abstraction is introduced.
- Automatically renew the lease of every running handler at a bounded interval.
- Keep handler progress reporting as additional state, not the sole lease-renewal mechanism.
- Stop automatic heartbeats before terminal state is written.
- Let a crashed worker's lease expire and remain reclaimable.
- Preserve cancellation, timeout, retry, scope-context, and ownership checks.

### First Implementation

- Limit each claim to currently available execution slots.
- Use round-robin worker-group selection across claim cycles.
- Start claimed leases immediately and release capacity only after processing finishes.
- Add a worker-owned heartbeat loop using `ITaskRuntimeReporter` while a handler is running.
- Add validated heartbeat interval configuration derived from or bounded by lease duration.
- Reject configurations where heartbeat cannot safely renew the lease.

Do not add product-specific worker groups or a distributed scheduler.

### Proof

- two workers, one slow handler, no duplicate concurrent execution;
- `BatchSize > MaxConcurrency` never creates waiting expired leases;
- later worker groups make progress while an earlier group is busy;
- a running task remains owned beyond its original lease through automatic heartbeat;
- a killed worker stops heartbeating and its run is reclaimable;
- cancellation and handler timeout stop heartbeat activity;
- PostgreSQL and SQL Server task stores preserve ownership semantics.

## Slice 2: Messaging Journal Lifecycle

### Problem

Processed outbox and inbox rows currently remain indefinitely. Their growth affects storage, indexes, vacuum/maintenance, backups, and sensitive-data recovery obligations. Failed and exhausted records must remain inspectable and must not be silently removed.

Inbox cleanup is constrained by transport replay: removing a processed inbox marker before the broker can replay the corresponding event weakens duplicate-delivery protection.

### Required Behavior

- Add explicit, independently configurable retention for processed outbox and inbox rows.
- Keep failed, processing, pending, locked, and exhausted records unless a separate explicit policy permits removal.
- Delete in bounded batches with cancellation support.
- Avoid long-running cleanup transactions.
- Expose cleanup duration, deleted rows, failures, and oldest retained terminal row.
- Keep cleanup optional and safe-disabled by default for compatibility.
- Let production composition require an explicit retain-or-clean decision.
- Document and validate the inbox replay-horizon invariant.

### API Direction

Prefer messaging-owned contracts rather than a universal retention framework:

- module-qualified cleanup stores alongside existing module-qualified inbox/outbox stores;
- shared EF implementations that modules can inherit without duplicating SQL semantics;
- hosted cleanup orchestration in messaging infrastructure;
- options for interval, batch size, processed-outbox retention, and processed-inbox retention;
- no product legal-hold or archive API in GMA.

### Proof

- only eligible terminal rows are removed;
- locked/pending/failed/exhausted rows survive cleanup;
- cleanup is module-qualified and cannot operate on another module's schema accidentally;
- repeated cleanup is idempotent;
- concurrent publisher/consumer activity remains correct;
- provider-backed tests cover PostgreSQL and SQL Server SQL translation;
- production validation detects an inbox retention shorter than the declared replay horizon.

## Slice 3: JetStream Safety

### Problem

The current NATS adapter creates a stream with a name and subjects but no explicit age, byte, count, message-size, storage, or replica policy. Existing streams are accepted without validating their effective configuration. Consumer `AckWait` can equal handler timeout without in-progress acknowledgements.

### Required Behavior

- Support explicit `Managed` and `External` stream-management modes.
- In managed mode, create or update the stream from validated options.
- In external mode, inspect and validate the existing stream without mutating it.
- Support bounded `MaxAge`, `MaxBytes`, optional message/count limits, message-size limit, storage, discard behavior, and replicas where the client/server support them.
- Production validation must reject an unintentionally unbounded managed stream.
- Keep development opt-out explicit.
- Send periodic in-progress acknowledgements while a handler is active, or validate that `AckWait` safely exceeds the maximum handler window.
- Preserve durable naming, subject validation, publish deduplication, and at-least-once delivery.

GMA must not infer payload sensitivity or application retention law.

### Proof

- managed stream creation is bounded and idempotent;
- managed updates change only supported mutable settings;
- external mode detects incompatible subjects and unsafe limits;
- a slow valid handler is not concurrently redelivered while reporting progress;
- a dead handler stops progress acknowledgements and is redelivered;
- poison-message and max-delivery behavior remains deterministic;
- stream and inbox retention validation uses the same declared replay horizon.

## Slice 4: TaskRuntime History Lifecycle

### Required Behavior

- `Gma.Modules.TaskRuntime` owns cleanup of terminal task runs and completed control messages.
- Active, queued, retry-scheduled, leased, running, cancellation-requested, and unresolved control records are never removed.
- Successful/canceled and failed/timed-out histories may have separate retention periods.
- Cleanup uses bounded batches and indexed terminal timestamps.
- Cleanup remains optional and explicitly configured.
- Metrics report deleted rows, failures, and oldest eligible history.

Do not place TaskRuntime persistence rules in task-owning product modules.

## Slice 5: Throughput And Fairness

Implement only after the correctness and lifecycle slices are green.

- Allow different module outbox stores to make progress independently.
- Preserve ordered processing inside one module store by default.
- Add opt-in bounded subscription concurrency; default remains one.
- Do not promise global event ordering.
- Introduce partition-key ordering only behind an explicit generic contract and a demonstrated adopter need.
- Retain polling as the cross-process durability fallback.
- An in-process wake signal may reduce latency when writers and publishers share a process.

Do not add application aggregates, tenant ids, or payload values to metric tags.

## Slice 6: Production Conformance

Add reusable test coverage and skeleton proof for:

- multi-worker lease competition and reclaim;
- worker death during execution;
- broker outage, publisher retry, and duplicate acknowledgement;
- consumer failure, redelivery, and inbox deduplication;
- bounded journal cleanup during active processing;
- bounded JetStream storage configuration;
- slow-handler acknowledgement progress;
- separated API/worker composition;
- safe-disabled minimal composition;
- migration drift and source/package boundary guards.

Application repositories should add their own domain load scenarios instead of putting product workloads into GMA.

## Deferred Decisions

- A universal retention/legal-hold framework is not justified.
- A two-mode atomic versus leased-idempotent inbox API should be designed only when a real long-I/O handler proves the need.
- Per-group worker pools may follow the fair global scheduler if independent resource budgets are required.
- Generic connector/adapter packages should wait for a second adopter such as StayQuest.
- Database partitioning and deployment connection-pool sizes remain application/deployment decisions.

## Release And Integration Order

1. Implement and validate framework task-worker correctness on `dev`.
2. Implement and validate framework messaging lifecycle and NATS safety on `dev`.
3. Implement TaskRuntime persistence lifecycle against the released framework contracts.
4. Update the Skeleton composition, defaults, docs, architecture guards, and Docker tests.
5. Release/publish framework first, TaskRuntime second, Skeleton pointer last.
6. Consumer applications update only to published commits and run their full validation suites.

## Completion Gate

This task is complete only when:

- all required slices are implemented or explicitly excluded with rationale;
- framework and TaskRuntime unit/integration tests are green;
- provider migrations and drift checks are green;
- Docker-backed multi-worker, NATS, and cleanup tests are green;
- the Skeleton demonstrates minimal and production compositions;
- documentation and architecture catalogs match source;
- a consuming application has integrated the release without internal project references;
- no product-specific vocabulary or policy exists in GMA runtime packages.

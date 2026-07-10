# Production Readiness Backlog

This tracker records the long-running hardening backlog for the skeleton. Each item should end as either implemented with tests/docs or deliberately excluded with documented reasoning.

## Current Principles

- Preserve the small-core modular monolith direction.
- Keep modules optional and explicitly composed.
- Prefer contracts, events, local projections, and replaceable adapters over cross-module internals.
- Add magic only when it reduces meaningful maintenance cost and is guarded by tests/docs.
- Keep production-readiness work evidence-backed: architecture guards, targeted tests, docs, and scripts should move together.

## Backlog

| Item | Status | Notes |
| --- | --- | --- |
| Module composition features and profiles | Pending | Add a generic shared composition-feature model so module profiles, hosts, and cross-boundary adapter packages can declare provided/required features and fail fast when a selected module profile requires something that was not composed. See [Module Composition Features And Profiles Task](module-composition-features-task.md). |
| Contracts and folder structure | In progress | Public contracts now use `Api/`, `Admin/`, `Events/`, `Metadata/`, and `Types/`. Admin contract wrappers use `Permissions/` and `Operations/`. |
| Shared event abstractions | Implemented | Added tenant-neutral `IntegrationEvent`, scope-aware `ScopedIntegrationEvent`, `DomainEvent`, and `ScopedDomainEvent` base records, migrated Auth/Catalog events, and guarded module events from bypassing shared metadata validation. |
| Admin naming | Implemented | CLI-only module front doors use `.AdminCli`; shared typed permission/operation helpers stay in `.Admin.Contracts`, and HTTP admin front doors stay in `.AdminApi`. |
| Test organization and value audit | Implemented | Test files now live under intent folders, docs describe the taxonomy, and architecture guards enforce test categories, names, Docker traits, and folder placement. Follow-up notes capture the remaining long-term split and coverage watchpoints. |
| Code magic/reflection | Implemented | Added constrained module-application assembly registration for CQRS handlers, validators, and domain-event handlers; integration-event subscriptions remain explicit. ADR 0006 documents why this stays in-house instead of adopting broad scanning. |
| Validation library | Excluded | ADR 0007 keeps the shared CQRS validator contracts as the default. FluentValidation remains a future module-specific adapter option only if a real module needs its richer rule model. |
| Tasks/daemons framework | In progress | ADR 0008 adds shared task/daemon contracts, explicit task-handler registration, task metadata guards, scheduler-neutral run-store contracts, an optional EF-backed `TaskRuntime` module with SQL Server/PostgreSQL migrations, hosted worker composition, bounded metrics/logging, queue-depth and active-run gauges, lease renewal through heartbeat/progress, stale timeout scanning, retrying hosted loops for transient runtime failures, optional code-defined scheduling, admin CLI/API controls/stats/control messages, and a compiled `TaskSamples` example with retries/progress/versioning/cooperative pause/resume/cancellation. Live status streaming, provider-level stress testing, and external scheduler adapters remain future optional slices. |
| Background worker host | First slice implemented | Added optional `Host.Worker` with safe-disabled defaults, config-gated explicit module groups, configured NATS publishing/consumer adapters, optional TaskRuntime worker composition, AppHost opt-in separated publishing, architecture/startup tests, and a Docker-backed Auth API write -> worker publish proof. Remaining work: richer health checks for stuck backlog, operational backlog read models, provider stress tests for larger worker fleets, and deployment-specific connection-pool tuning. See [Background Worker Host Task](background-worker-host-task.md). |
| Projection rebuild tasks | Implemented | ADR 0010 adds `Gma.Framework.ProjectionRebuild`, consumer-owned checkpoint stores, task progress and bounded metrics, provider migrations for Ordering checkpoints, and a compiled Catalog-to-Ordering rebuild example. Full-rebuild/tombstone policies and high-water-mark catch-up remain future optional slices. |
| Notifications and realtime streaming | Implemented | ADR 0011 and ADR 0012 add optional front-door notification contracts, a generic realtime fanout layer, a notification-to-realtime bridge, SSE and SignalR adapters, persisted notification history/read state, admin history access, a durable Notifications request event, bounded queues, fail-open delivery, metrics, architecture guards, and targeted streaming tests. Preferences, delivery receipts, retention cleanup, and multi-instance live backplanes remain future optional slices. |
| Shared access subject foundation | Implemented | `Gma.Framework.AccessControl` provides backend-agnostic `AccessSubject` primitives while module-owned domain visibility scopes handle list/detail filtering. Generic authorization uses explicit permission/scope requirements instead of hidden resource filtering. |
| Generic access control and RBAC | Implemented in source-first skeleton | `Gma.Framework.AccessControl` owns generic permission/scope/requirement/decision contracts, `Gma.Framework.AccessControl.AspNetCore` adds explicit endpoint enforcement, `Gma.Modules.AccessControl` owns persisted RBAC and SQL Server/PostgreSQL migrations, and Administration now manages AccessControl through CLI/API front doors while keeping audit. Remote repo/submodule packaging remains a source-management follow-up. See [Generic Access Control And RBAC Refactor Task](generic-access-control-rbac-task.md). |
| File storage | Pending | Design optional file storage contracts and first real adapter, likely MinIO/S3-compatible if it fits. |
| External auth | Pending | Add provider-based login/registration, account linking, and migration paths while keeping Auth reusable. |

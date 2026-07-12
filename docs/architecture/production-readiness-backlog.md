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
| Module composition features and profiles | Implemented | Shared composition features/profiles fail fast for missing or conflicting adapters; generated hosts validate composition and start under a real smoke test. See [Module Composition Features And Profiles Task](module-composition-features-task.md). |
| Contracts and folder structure | In progress | Public contracts now use `Api/`, `Admin/`, `Events/`, `Metadata/`, and `Types/`. Admin contract wrappers use `Permissions/` and `Operations/`. |
| Shared event abstractions | Implemented | Added tenant-neutral `IntegrationEvent`, scope-aware `ScopedIntegrationEvent`, `DomainEvent`, and `ScopedDomainEvent` base records, migrated Auth/Catalog events, and guarded module events from bypassing shared metadata validation. |
| Admin naming | Implemented | CLI-only module front doors use `.AdminCli`; shared typed permission/operation helpers stay in `.Admin.Contracts`, and HTTP admin front doors stay in `.AdminApi`. |
| Test organization and value audit | Implemented | Test files now live under intent folders, docs describe the taxonomy, and architecture guards enforce test categories, names, Docker traits, and folder placement. Follow-up notes capture the remaining long-term split and coverage watchpoints. |
| Code magic/reflection | Implemented | Added constrained module-application assembly registration for CQRS handlers, validators, and domain-event handlers; integration-event subscriptions remain explicit. ADR 0006 documents why this stays in-house instead of adopting broad scanning. |
| Validation library | Excluded | ADR 0007 keeps the shared CQRS validator contracts as the default. FluentValidation remains a future module-specific adapter option only if a real module needs its richer rule model. |
| Tasks/daemons framework | Implemented foundation | ADR 0008 provides explicit handlers, scheduler-neutral stores, provider-backed leases/retries/heartbeats/timeouts, queue/active gauges, admin controls, Docker provider proofs, and compiled samples. External scheduler adapters, fleet-size stress envelopes, and connection-pool tuning remain deployment/product exercises rather than neutral defaults. |
| Durable runtime hardening | Active | Capacity-aware fair task claiming, automatic worker heartbeats, messaging journal lifecycle, bounded JetStream management, TaskRuntime history cleanup, and failure-oriented conformance proof are tracked in [Durable Runtime Hardening](durable-runtime-hardening-task.md). |
| Background worker host | First slice implemented | Added optional `Host.Worker` with safe-disabled defaults, config-gated explicit module groups, configured NATS publishing/consumer adapters, optional TaskRuntime worker composition, AppHost opt-in separated publishing, architecture/startup tests, and a Docker-backed Auth API write -> worker publish proof. Remaining work: richer health checks for stuck backlog, operational backlog read models, provider stress tests for larger worker fleets, and deployment-specific connection-pool tuning. See [Background Worker Host Task](background-worker-host-task.md). |
| Projection rebuild tasks | Implemented | ADR 0010 adds `Gma.Framework.ProjectionRebuild`, consumer-owned checkpoint stores, task progress and bounded metrics, provider migrations for Ordering checkpoints, and a compiled Catalog-to-Ordering rebuild example. Full-rebuild/tombstone policies and high-water-mark catch-up remain future optional slices. |
| Notifications and realtime streaming | Implemented foundation | Includes durable history/read state, admin access, SSE/SignalR, bounded queues, metrics, replaceable preference evaluation, and opt-in bounded retention. Delivery receipts and multi-instance live backplanes remain product/host adapters; durable history does not depend on them. |
| Shared access subject foundation | Implemented | `Gma.Framework.AccessControl` provides backend-agnostic `AccessSubject` primitives while module-owned domain visibility scopes handle list/detail filtering. Generic authorization uses explicit permission/scope requirements instead of hidden resource filtering. |
| Generic access control and RBAC | Implemented in source-first skeleton | `Gma.Framework.AccessControl` owns generic permission/scope/requirement/decision contracts, `Gma.Framework.AccessControl.AspNetCore` adds explicit endpoint enforcement, `Gma.Modules.AccessControl` owns persisted RBAC and SQL Server/PostgreSQL migrations, and Administration now manages AccessControl through CLI/API front doors while keeping audit. Remote repo/submodule packaging remains a source-management follow-up. See [Generic Access Control And RBAC Refactor Task](generic-access-control-rbac-task.md). |
| File storage | Implemented foundation | Backend-neutral contracts, LocalStorage and MinIO adapters, a private Files API, validated metadata/limits, and a fail-closed replaceable content-inspection seam are implemented. Product modules own reference-aware retention/legal holds. |
| External auth | Product adapter boundary | Auth intentionally does not trust raw external identifiers or ship a universal callback. Products add explicit OIDC/MFA/recovery adapters that validate provider assertions/challenges before invoking reusable Auth behavior; Google/email vendors and recovery policy remain product-owned. |
| Production HTTP edge | Implemented | Optional production adapter covers host filtering, trusted proxies, ProblemDetails, HTTPS/HSTS, security headers, CORS, timeouts, rate limits, private admin boundaries, and liveness/readiness separation. |
| Security rotation and concurrency | Implemented foundation | Auth supports password blocklist replacement, account throttling, rehash persistence, JWT/pepper key rings, refresh reuse revocation, optimistic concurrency, and provider migrations; AccessControl bootstrap is atomic. |
| Operations and release provenance | Implemented foundation | EF readiness checks, outbox backlog/exhausted/age metrics, provider-explicit migration tools, cross-platform CI, automatic Docker cadence, dependency automation, pinned actions, and release source-set manifests are present. Deployment alert thresholds/backups/capacity remain app-owned. |

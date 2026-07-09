# Deployment Guidelines

This skeleton is a modular monolith. Deploy one API process, but preserve module boundaries inside it.

## Runtime Dependencies

Current dependencies:

- SQL Server or PostgreSQL
- NATS with JetStream
- ASP.NET Core hosting environment
- secret/config provider for JWT signing key and connection strings
- Redis only when `Caching:Enabled=true` and `Caching:Provider=Redis`
- realtime-backed SignalR/SSE notification streaming only when `Notifications:Enabled=true`
- notification history tables only when the optional `Notifications` module is composed
- `Host.Worker` only when background publishing, consumers, inbox/projection handling, or task workers should run outside HTTP hosts

## Configuration

Required production configuration:

- `ApplicationIdentity:DisplayName`
- `ApplicationIdentity:Namespace`
- `Persistence:Provider`
- provider connection string
- `ConnectionStrings:nats`
- `Auth:Jwt:SigningKey`
- `Auth:RefreshTokens:Pepper`
- `Auth:RefreshTokenLifetimeDays`

Administration bootstrap, tenancy, admin API, outbox, NATS JetStream, NATS consumer, notifications, caching, Redis, observability, JWT, and refresh-token settings are validated at startup. Persistence settings are validated when a persisted module is composed. Treat validation failures as deployment misconfiguration rather than runtime warnings.

Recommended production configuration:

- `Outbox:BatchSize`
- `Outbox:PollIntervalMilliseconds`
- `Outbox:LockDurationMilliseconds`
- `Outbox:MaxAttempts`
- `NatsJetStream:Enabled`
- optional `NatsJetStream:StreamName` only when broker naming must differ from `ApplicationIdentity:Namespace`
- `ConnectionStrings:nats` when JetStream publishing is enabled
- `NatsConsumers:Enabled` only for hosts that explicitly register consumers
- optional `NatsConsumers:DurablePrefix` only when durable names must differ from `ApplicationIdentity:Namespace`
- `NatsConsumers:FetchBatchSize`
- `NatsConsumers:PollInterval`
- `NatsConsumers:AckWait`
- `NatsConsumers:MaxDeliver`
- `NatsConsumers:HandlerTimeout`
- `NatsConsumers:NakDelay`
- `Worker:Modules:Auth` for workers that drain Auth outbox rows
- `Worker:Modules:Catalog` for workers that drain Catalog outbox rows or provide Catalog projection export sources
- `Worker:Modules:Ordering` for workers that consume Catalog events or run Ordering projection rebuild tasks
- `Worker:Modules:TaskRuntime` for workers that execute persisted task runs
- `Worker:Modules:TaskSamples` only for sample/demo task execution
- `Tasks:Worker:Enabled` only for worker processes that should claim task runs
- `Tasks:Worker:WorkerGroups`
- `Tenancy:Enabled`
- `Tenancy:HeaderName`
- `Tenancy:LocalDefaultTenantId`
- `Observability:Prometheus:Enabled`
- `Observability:Prometheus:EndpointPath`
- `Observability:Otlp:Enabled`
- `Observability:Otlp:Endpoint`
- `Observability:Otlp:ExportMetrics`
- `Observability:Otlp:ExportTraces`
- `Observability:Otlp:ExportLogs`
- `Caching:Enabled`
- `Caching:Provider`
- `Caching:DefaultDistributedExpiration`
- `Caching:DefaultLocalExpiration`
- `Caching:MaximumPayloadBytes`
- `Caching:MaximumKeyLength`
- optional `Caching:KeyPrefix` only when cache storage must differ from `ApplicationIdentity:Namespace`
- `Caching:Redis:ConnectionName` when Redis is selected
- `ConnectionStrings:redis` when Redis is selected
- optional `Caching:Redis:InstanceName` only when Redis itself needs an extra provider-level prefix
- `Administration:Bootstrap:AllowWhenAssignmentsExist`
- `Administration:Bootstrap:OwnerRoleName`
- `Administration:Api:ActorIdClaim`
- `Administration:Api:TenantIdClaim`
- `Administration:Api:RequireTenantClaimMatch`
- `Administration:Api:AllowGeneratedPasswordResponses`
- optional `Auth:Jwt:Issuer` and `Auth:Jwt:Audience` only when they must differ from `ApplicationIdentity:DisplayName`
- `Notifications:Enabled`
- `Notifications:SubscriberQueueCapacity`
- `Notifications:MaximumPayloadBytes`
- `Notifications:Sse:Enabled`
- `Notifications:Sse:StreamPath`
- `Notifications:Sse:HeartbeatInterval`
- `Notifications:SignalR:Enabled`
- `Notifications:SignalR:HubPath`
- `Notifications:SignalR:ClientMethodName`

Never use checked-in development JWT signing keys, refresh-token peppers, or database passwords in production. Auth option classes intentionally have no secret defaults; local placeholders live only in development configuration. The JWT signing key and refresh-token pepper are both validated for minimum shape at startup, but secret rotation and storage are still deployment responsibilities.

## Migrations

Do not auto-apply migrations from `Host.Api` startup.

Build deployable production artifacts from `main` or an explicit release tag. The `dev` branch is the normal integration branch for in-progress feature work and can be used for local or staging validation, but it is not the default production baseline.

Recommended deployment flow:

1. Build artifact.
2. Run provider-specific migrations as an explicit deployment step.
3. Start or roll the API.
4. Verify health checks.

Each module with persistence owns its migrations.

## Health Checks

The API maps:

```text
/health
/alive
```

Service defaults may expose additional observability endpoints depending on environment.

Prometheus scraping is exposed at the configured path only when enabled.

For centralized logs, send OTLP logs to Grafana Alloy or an OpenTelemetry Collector and forward them to Loki. Do not configure Loki-specific dependencies in modules.

## Caching

Caching is disabled by default and must remain an optimization. Redis configuration errors fail startup; runtime outages fail open to the authoritative source.

When enabling Redis:

- provision bounded memory and an eviction policy appropriate for disposable data;
- monitor `{ApplicationIdentity:Namespace}.cache.backend.failures` and `{ApplicationIdentity:Namespace}.cache.invalidation.failures`;
- keep TTLs bounded so failed invalidation self-recovers;
- disable L1 or shorten local TTL for entries that need faster cross-node coherence;
- never use cache contents for authorization or tenant resolution.

## Notifications

Notifications are disabled by default and are best-effort front-door delivery. They do not replace outbox/NATS for durable integration facts.

When enabling notifications:

- compose `AddUserNotificationsCqrs()` in hosts whose command handlers enqueue notification requests;
- compose `AddUserNotificationsRealtime()` in hosts that expose live notification streams;
- confirm clients use authenticated SSE at `Notifications:Sse:StreamPath` or SignalR at `Notifications:SignalR:HubPath`;
- keep `Notifications:MaximumPayloadBytes` small enough to prevent accidental large live payloads;
- size `Notifications:SubscriberQueueCapacity` for the expected number of slow clients;
- do not use notification delivery as the only record of an operation;
- monitor `{ApplicationIdentity:Namespace}.notifications.published`, `{ApplicationIdentity:Namespace}.notifications.delivered`, and delivery failures;
- for multiple API instances, use sticky sessions, an explicit SignalR backplane/Azure SignalR slice, or persisted history/replay if missed live messages are not acceptable.
- if composing the `Notifications` module, run its provider-specific migrations and treat history as a user-facing read model, not the source of authorization or business truth;
- if using durable notification requests, compose the Notifications consumer runtime and verify producer modules publish `{ApplicationIdentity:Namespace}.{producer-module}.user-notification-requested.v1` through their outbox.

## Background Processing

Simple deployments can keep background publishing inside the API process:

```text
Host.Api:
  NatsJetStream:Enabled=true
  NatsConsumers:Enabled=false

Host.Worker:
  not deployed
```

Separated production deployments keep HTTP hosts request-focused and run background pressure in worker replicas:

```text
Host.Api:
  NatsJetStream:Enabled=false
  NatsConsumers:Enabled=false

Host.Worker:
  Worker:Modules:Auth=true
  NatsJetStream:Enabled=true
  NatsConsumers:Enabled=false
```

Enable consumers and task workers only in worker replicas that compose the required module stores and handlers:

```text
Host.Worker:
  Worker:Modules:Catalog=true
  Worker:Modules:Ordering=true
  Worker:Modules:TaskRuntime=true
  NatsConsumers:Enabled=true
  Tasks:Worker:Enabled=true
  Tasks:Worker:WorkerGroups:0=projection-workers
```

The outbox publisher runs as a hosted service in whichever process composes configured NATS publishing and module outbox stores.

Operational expectations:

- multiple instances can claim messages safely;
- stale locks can be reclaimed;
- failed messages retry until max attempts;
- exhausted messages require operational review.
- run module migrations before starting worker replicas that query those module schemas;
- size worker database connection pools separately from API pools;
- tune outbox batch size and poll interval for backlog and database pressure;
- tune NATS consumer batch size, ack wait, max deliver, handler timeout, and nak delay for handler behavior;
- keep at least one worker replica healthy before disabling API-side publishing;
- rollback by re-enabling API-side publishing or rolling forward with replacement worker replicas.

## Tenancy

For tenant-enabled deployments:

- ensure clients always send `X-Tenant-Id`;
- prefer tokens with tenant claims for tenant-bound actors;
- keep `Administration:Api:RequireTenantClaimMatch=true` so present admin tenant claims must match the requested tenant;
- if an identity provider cannot issue tenant claims, document that RBAC assignments are the authoritative tenant boundary for admin API calls;
- monitor failed auth attempts caused by tenant mismatches.

## Rollback

Prefer backward-compatible changes:

- additive columns;
- nullable fields before required fields;
- additive integration event fields;
- versioned event subjects.

Avoid deploying code that requires a migration that has not run yet.

## CI Validation

Minimum CI path:

```powershell
.\eng\restore.ps1
.\eng\build.ps1 -NoRestore
.\eng\test-fast.ps1 -NoBuild
```

Infrastructure CI path:

```powershell
.\eng\test-docker.ps1 -NoBuild
```

Run Docker-backed tests in CI with Docker available.

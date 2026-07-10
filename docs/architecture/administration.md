# Administration

Administration is an optional first-class capability. It has CLI and HTTP front doors, both composed outside the public API host.

## Goals

- Keep `Host.Api` focused on public HTTP APIs.
- Make admin surfaces explicit host choices.
- Keep command parsing and HTTP mapping outside domain and application code.
- Persist admin audit only when the optional Administration module is present.
- Use the optional `Gma.Modules.AccessControl` module and `Gma.Framework.Administration.AccessControl` bridge for persisted RBAC.
- Let feature modules expose admin commands without depending on another module's internals.

## Projects

Shared contracts:

```text
Gma.Framework.Administration
Gma.Framework.Administration.AccessControl
Gma.Framework.Administration.Api
Gma.Framework.Administration.Cli
```

Optional host:

```text
Host.AdminCli
Host.AdminApi
```

Optional persisted RBAC module:

```text
Gma.Modules.AccessControl.Application
Gma.Modules.AccessControl.Persistence
Gma.Modules.AccessControl.AdminCli
Gma.Modules.AccessControl.AdminApi
Gma.Modules.AccessControl.Persistence.SqlServerMigrations
Gma.Modules.AccessControl.Persistence.PostgreSqlMigrations
```

Optional persisted audit and administration front-door module:

```text
Gma.Modules.Administration.Application
Gma.Modules.Administration.Persistence
Gma.Modules.Administration.Persistence.SqlServerMigrations
Gma.Modules.Administration.Persistence.PostgreSqlMigrations
Gma.Modules.Administration.AdminCli
Gma.Modules.Administration.AdminApi
```

Feature admin front doors:

```text
Gma.Modules.Auth.Admin.Contracts
Gma.Modules.Auth.AdminCli
Gma.Modules.Auth.AdminApi
```

## Shared Contracts

`Gma.Framework.Administration` contains backend-agnostic administration concepts:

- `AdminPermission`
- `AdminOperation`
- `AdminActor`
- `IAdminActorContextAccessor`
- `IAdminAuthorizationService`
- `IAdminAuditSink`

The shared core also owns the admin operation runner used by CLI and HTTP front doors for actor context, tenant context, authorization, execution, and audit.
It does not own command-line parsing, HTTP mapping, or host-builder module contracts.
`AdminPermission` validates normal dot-separated permission codes directly; the legacy `*` owner wildcard remains Administration-specific compatibility behavior.
The default admin authorization service denies by default, and the default audit sink is a no-op. This keeps admin support optional until a host composes real RBAC and audit.
`Gma.Framework.Administration.AccessControl` is the explicit bridge that adapts `IAdminAuthorizationService` to generic `IAccessAuthorizationService`.
Custom `IAdminAuthorizationService` implementations remain supported and should return `AdminAuthorizationResult` through `Allowed()` or `Denied(reason)`. Allowed results carry no failure reason, and denied results carry a bounded, normalized operator-facing reason.

## CLI Adapter

`Gma.Framework.Administration.Cli` is the only shared project that knows about `System.CommandLine`.

It owns:

- `IAdminCliModule`;
- global options: `--actor`, `--tenant`, `--output`;
- command registration through `IAdminCliCommandRegistry`;
- tenant setup before dispatch;
- authorization before mutation/query execution;
- audit recording after authorization decisions and command results;
- exit-code mapping.

Feature modules should not parse command-line arguments directly outside their `.AdminCli` project.
Admin permission code strings live in public `.Contracts` so module metadata can declare them without referencing admin-only packages. Typed `AdminPermission` wrappers shared by CLI and HTTP live in `.Admin.Contracts`.
`Host.AdminCli` loads its copied tool/app output configuration by using `AppContext.BaseDirectory` as the content root. It validates configured startup options before parsing commands, but it does not start hosted services. Long-running publishers, consumers, and HTTP endpoints therefore remain outside the CLI process.

## API Adapter

`Gma.Framework.Administration.Api` is the only shared admin project that knows about ASP.NET Core HTTP mapping.

It owns:

- admin API module registration;
- actor resolution from authenticated claims;
- tenant resolution for tenant-scoped admin operations;
- HTTP result mapping;
- audit failure response headers.

Feature modules should not map admin HTTP endpoints outside their `.AdminApi` project.
Admin API endpoints should call `AdminApiExecutor` for authorization, tenant enforcement, execution, audit, and expected error-to-status mapping. Do not use generic tenant endpoint filters on admin routes, because they can short-circuit audit recording.

Use `ApiErrorStatusCodeMap` at the admin API front door for expected operation outcomes such as not found or conflict. Domain and application errors stay HTTP-agnostic; the executor keeps authorization, tenant, audit, and unexpected-failure status mapping centralized.

## Host Composition

`Host.AdminCli` is a packable .NET tool with command name:

```text
gma-admin
```

It explicitly composes admin modules:

```csharp
builder.AddGmaAdministrationCli();
builder.AddRedisCaching(); // no-op unless Redis caching is enabled
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddMessagingInfrastructure(); // outbox writer registry without hosted publishers
builder.AddAdminModule<AdministrationAdminCliModule>();
builder.AddAdminModule<AccessControlAdminCliModule>();
builder.AddAdminModule<AuthAdminCliModule>();
```

It does not map HTTP endpoints and does not start long-running publishers or consumers. Admin operations are short-lived command executions.

`Host.AdminApi` is a separate optional web host. It maps admin HTTP endpoints, requires authentication, and composes only the admin API modules the project wants.

HTTP bootstrap is intentionally not exposed. Use the AccessControl CLI bootstrap command for first-owner setup, then use the AccessControl admin API for normal RBAC and module administration.
`AccessControl:Bootstrap:OwnerRoleName` is validated at startup and must be a valid access-control role-name slug.

`Host.Api` does not register admin API modules.

## RBAC

The AccessControl module stores:

- principals;
- roles;
- role permissions;
- principal role assignments.

The Administration module stores admin operation audit entries.

Permission codes are declared by modules in code. Role and assignment data is persisted by permission code.

Examples:

```text
admin.roles.manage
auth.members.read
auth.members.disable
*
```

`*` is the owner wildcard grant. Authorization is deny-by-default in the core admin package. When `Gma.Framework.Administration.AccessControl` and `Gma.Modules.AccessControl` are composed, authorization flows through generic `IAccessAuthorizationService` plus the AccessControl persisted decision provider. Global assignments and tenant-scoped assignments are stored as normalized access scopes, such as `global` and `tenant:default`.

## Audit

Audit records include:

- actor id;
- tenant id, when present;
- operation name;
- permission code;
- result;
- error code;
- timestamp.

Audit records must never include passwords, tokens, token hashes, raw refresh tokens, or other secrets. Audit write failures are reported to CLI stderr but do not roll back already committed domain mutations.

Actor ids are external identifiers resolved from CLI `--actor` or authenticated claims. They are trimmed, case-preserving, capped at 256 characters, and cannot contain whitespace or control characters. Admin API requests with an invalid actor claim fail as unauthorized before RBAC or audit recording, because there is no trustworthy actor id to audit against.
Audit error codes are bounded operation metadata. They should be stable application or domain error codes, not free-form messages.

Generated admin passwords are never audited. The admin API does not return generated passwords unless `Administration:Api:AllowGeneratedPasswordResponses=true` is explicitly configured. When disabled, admin API callers should provide a password through their own secure channel or use the CLI `--generate-password` flow.

## Tenant Scope

Tenant-scoped admin commands require `--tenant` when tenancy is enabled. The CLI executor sets `ITenantContextAccessor` before dispatching CQRS handlers, so module repositories and EF filters keep their usual tenant isolation behavior.

Tenant-scoped admin HTTP endpoints require the configured tenant header, normally:

```http
X-Tenant-Id: default
```

`AdminApiExecutor` reads the header, passes the tenant through RBAC, and sets tenant context before dispatch. Missing tenant headers are recorded through the admin audit path.

Admin API tenant binding is configurable under `Administration:Api`:

```json
{
  "ActorIdClaim": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
  "TenantIdClaim": "scope_id",
  "RequireTenantClaimMatch": true
}
```

When `RequireTenantClaimMatch` is `true`, tenant-scoped admin HTTP operations compare the requested tenant with the configured token scope claim if that claim is present. A present mismatch fails before RBAC with `Admin.TenantClaimMismatch` and is audited. A missing scope claim is allowed so external identity providers or global operator tokens can still work; RBAC must still grant the actor permission for the requested tenant.
Admin API options are validated at startup: `ActorIdClaim` is required, and `TenantIdClaim` is required when tenant-claim matching is enabled.

Global RBAC assignments have no tenant scope. Tenant-scoped assignments can administer only the matching tenant.

## Command Flow

```text
gma-admin
  -> Host.AdminCli
  -> IAdminCliModule.MapCommands
  -> AdminCliExecutor
  -> IAdminOperationRunner
  -> actor and tenant context
  -> IAdminAuthorizationService
  -> optional AccessControl bridge
  -> IRequestDispatcher
  -> module command/query handler
  -> module unit of work
  -> domain events and outbox
  -> IAdminAuditSink
```

HTTP flow:

```text
HTTP request
  -> Host.AdminApi
  -> IAdminApiModule.MapEndpoints
  -> AdminApiExecutor
  -> IAdminOperationRunner
  -> actor and tenant context
  -> IAdminAuthorizationService
  -> optional AccessControl bridge
  -> IRequestDispatcher
  -> module command/query handler
  -> module unit of work
  -> domain events and outbox
  -> IAdminAuditSink
```

## Rules

- `Host.Api` should not register admin modules.
- Only `.AdminCli`, `Gma.Framework.Administration.Cli`, and `Host.AdminCli` may reference `System.CommandLine`.
- Only `.AdminApi`, `Gma.Framework.Administration.Api`, and `Host.AdminApi` should map admin HTTP routes.
- Admin modules may call their own module application layer and shared admin contracts.
- Admin modules must not contain business rules or EF code.
- Core `Gma.Framework.Administration` must not reference `Gma.Framework.AccessControl`; use `Gma.Framework.Administration.AccessControl` for that integration.
- The Administration module must not reference Auth internals.
- Destructive commands should require `--yes`.
- Password input should use hidden prompt, `--password-stdin`, or `--generate-password`.
- Admin API route delegates should avoid validation returns before `AdminApiExecutor` for auditable operations. Put confirmation/password-source validation inside the executor action so authorization and audit happen first.
- Role/permission management commands should return application errors for invalid operator input. Do not let permission parsing exceptions escape into the unexpected-failure path.
- Admin role names are lowercase slugs: letters, numbers, and hyphens only, starting with a letter.
- Keep `Administration:Api:RequireTenantClaimMatch=true` unless the deployment's identity provider cannot issue tenant-bound admin tokens. If disabled, rely on RBAC assignments and gateway policy to prevent tenant confusion.
- Do not add an admin HTTP bootstrap endpoint without a separate ADR and architecture tests.

## Current Commands

```powershell
.\eng\run-admin.ps1 -- admin bootstrap --actor owner --yes
.\eng\run-admin.ps1 -- admin roles create --actor owner --name support
.\eng\run-admin.ps1 -- admin roles grant --actor owner --role support --permission auth.members.read
.\eng\run-admin.ps1 -- admin roles assign --actor owner --target-kind admin-actor --target-id support --role support --scope tenant:default
.\eng\run-admin.ps1 -- admin roles list --actor owner
```

```powershell
.\eng\run-admin.ps1 -- auth members list --actor support --tenant default
.\eng\run-admin.ps1 -- auth members get --actor support --tenant default --member-id <id>
.\eng\run-admin.ps1 -- auth members create --actor support --tenant default --username user@example.com --username-type email --generate-password
.\eng\run-admin.ps1 -- auth members disable --actor support --tenant default --member-id <id> --reason "support request" --yes
.\eng\run-admin.ps1 -- auth members enable --actor support --tenant default --member-id <id>
.\eng\run-admin.ps1 -- auth members reset-password --actor support --tenant default --member-id <id> --generate-password
.\eng\run-admin.ps1 -- auth members revoke-sessions --actor support --tenant default --member-id <id> --yes
```

## Current HTTP Endpoints

AccessControl:

```text
GET  /api/admin/roles
POST /api/admin/roles
POST /api/admin/roles/{roleName}/permissions
DELETE /api/admin/roles/{roleName}/permissions/{permissionCode}
POST /api/admin/roles/{roleName}/assignments
GET  /api/admin/roles/{roleName}/assignments
DELETE /api/admin/roles/{roleName}/assignments?subjectKind=<kind>&subjectId=<id>&scope=<scope>
```

Auth:

```text
GET  /api/admin/auth/members
GET  /api/admin/auth/members/{memberId}
POST /api/admin/auth/members
POST /api/admin/auth/members/{memberId}/disable
POST /api/admin/auth/members/{memberId}/enable
POST /api/admin/auth/members/{memberId}/reset-password
POST /api/admin/auth/members/{memberId}/revoke-sessions
```

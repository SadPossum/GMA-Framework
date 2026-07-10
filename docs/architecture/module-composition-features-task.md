# Module Composition Features And Profiles Task

This is an implementation task brief for a future architecture slice. It should be handed to an agent together with the current repository checkout.

## Implementation Status

Initial slice implemented:

- `Gma.Framework.ModuleComposition` owns validated profile/feature/module requirement primitives and host validation extensions.
- Auth exposes `AuthProfile.Global(...)` and `AuthProfile.ScopeAware()` through public contracts and profile-aware API/Admin API/Admin CLI front doors.
- Tenancy exposes a default profile that provides `tenancy.context` and `tenancy.header-resolution`; shared tenancy infrastructure provides the baseline `tenancy.context` service for non-HTTP/admin composition.
- Runtime hosts validate composition explicitly with `ValidateModuleComposition()`.
- Follow-up slice implemented package-owned feature catalogs for caching, messaging, notifications, and tasks; shared adapters now advertise concrete capabilities through composition metadata.
- Catalog, Ordering, Notifications, and TaskRuntime expose default profiles in their `.Contracts/Metadata` folders, and their public/admin front doors select those profiles explicitly.
- Auth global-profile tests now prove composition with `Gma.Framework.Infrastructure` and without `TenancyModule`; the profile uses the shared null/default scope context and stores the configured global scope in the existing tenant/scope column.
- Remaining follow-up work is mostly optional worker-host examples, broader API/Admin API/Admin CLI global-profile smoke coverage when those hosts are made tenant-free, and any future decoupling of tenant-shaped metadata from outbox/inbox/task records.

## Summary

Add a generic shared composition-feature model so modules can declare what they provide, what they require, and which profile is being composed.

The goal is not to create many duplicate module declarations. The goal is to let reusable modules own their adaptation boundary while shared packages stay small and optional.

Example:

```csharp
builder.AddAuthModule(AuthProfile.Global("global"));
builder.AddAuthModule(AuthProfile.ScopeAware());
```

The Auth example is illustrative only. The shared feature should not be Auth-specific, tenancy-specific, or messaging-specific. Auth, Catalog, Ordering, Notifications, TaskRuntime, and future modules should all be able to use the same composition vocabulary.

## Task Text

Implement a generic shared composition feature system for module profiles, provided capabilities, required capabilities, and fail-fast composition validation.

The first implementation should add small shared primitives for:

- stable feature identifiers;
- features provided by hosts, shared adapters, and module profiles;
- features required by module profiles;
- module dependencies required by a selected profile;
- profile identity and profile display/debug information;
- startup/composition validation with clear errors.

Then update at least one real module profile as the proof, preferably Auth, because Auth is the most reusable module and the best example of tenant-aware versus global behavior.

The first slice should not refactor every module and should not decouple every existing tenant-shaped shared type. It should establish the vocabulary and validation path, then document follow-up slices for tenancy/messaging/outbox decoupling.

## Problem

The skeleton already supports optional modules and optional infrastructure, but module compatibility is mostly implicit.

Today a host can compose:

```text
Auth + Tenancy + Messaging + NATS + TaskRuntime
```

or:

```text
Auth only
```

but there is no shared composition contract that says:

```text
Auth profile scope-aware requires scoping.context.
Auth profile global provides auth.members and uses the configured global scope id.
Catalog durable events require messaging.outbox.
Ordering projections require Catalog contracts plus messaging consumers when live projection updates are enabled.
Task handlers require tasks.worker only in hosts that should execute them.
```

The result is that feature assumptions leak through service registration, tenant context defaults, outbox registrations, endpoint filters, and documentation instead of being expressed as one validated composition graph.

## Current Repo Context

The repo already has useful building blocks:

- `ModuleDescriptor` has a generic `Features` list.
- Capability packages already add descriptor features such as permissions, published events, subscriptions, cache entries, tasks, and notifications.
- Runtime composition is explicit through host calls such as `AddModule<TModule>()`, `AddAdminApiModule<TModule>()`, `AddMessagingInfrastructure()`, `AddConfiguredNatsJetStreamMessaging()`, `AddNatsJetStreamConsumers()`, and `AddTaskWorkerRuntime()`.
- Optional cross-boundary packages already exist in spirit, for example `Gma.Framework.Caching.Cqrs`, `Gma.Framework.Notifications.Cqrs`, `Gma.Framework.Messaging.Nats.Aspire`, and `Gma.Framework.ProjectionRebuild.Tasks`.
- Existing docs already say metadata is not runtime discovery and that new optional capabilities should add `ModuleDescriptorFeature` subtypes rather than bloating the root descriptor.

The missing piece is a generic composition feature registry plus validation layer.

## Core Idea

Use a small shared composition model:

```text
Host/profile/adapters provide features.
Module profiles require features.
Module profiles may require other modules.
Composition validation runs after all intended modules/adapters are added.
Invalid combinations fail before serving traffic or running workers.
```

Examples:

```text
TenancyModule
  provides tenancy.context

Gma.Framework.Messaging.Infrastructure
  provides messaging.outbox.contracts

Configured NATS publishing adapter
  provides messaging.nats-publishing
  requires messaging.outbox.contracts

NATS consumer runtime
  provides messaging.nats-consumers
  requires messaging.nats-publishing

Auth global profile
  requires persistence
  provides auth.members
  uses auth.scope.global

Auth scope-aware profile
  requires persistence
  requires scoping.context
  provides auth.members
```

The exact names should be normalized and stable, but the implementation should not overfit the first examples.

## Design Principles

- Keep runtime composition explicit.
- Keep module profiles module-owned.
- Keep shared primitives generic.
- Prefer fail-fast validation over silent fallback.
- Keep default/simple projects usable.
- Do not let runtime configuration silently change schema shape.
- Keep feature ids stable, normalized, and useful in error messages.
- Distinguish required features from optional enhancements.
- Distinguish module dependency from package reference.
- Keep business rules in modules, not shared composition code.
- Keep descriptors metadata-only; descriptors must not register services or start workers.
- Add architecture tests so optional capabilities do not drag unrelated packages into base packages.

## Non-Goals

- Do not add assembly-wide module discovery.
- Do not create Spring-style broad auto-configuration.
- Do not make module metadata perform service registration.
- Do not require every module to define multiple profiles.
- Do not generate every profile combination for every module.
- Do not replace existing `ModuleDescriptorFeature` metadata such as permissions, events, subscriptions, cache entries, tasks, or notifications.
- Do not replace admin RBAC, task runtime, outbox/inbox, NATS consumers, or tenancy.
- Do not rename all existing `TenantId` columns in the first slice.
- Do not introduce a plugin marketplace or dynamic runtime module loading.

## Proposed Shared Package

Prefer a small package under shared modules/composition, for example:

```text
src/Framework/Gma.Framework.ModuleComposition
```

If the implementation fits better inside `Gma.Framework.Modules`, keep the public API separate enough that it can be moved later.

Suggested primitives:

```csharp
public readonly record struct CompositionFeatureId
{
    public CompositionFeatureId(string value);
    public string Value { get; }
}

public sealed record ProvidedFeature(
    CompositionFeatureId Id,
    string Provider,
    string? Description = null);

public sealed record RequiredFeature(
    CompositionFeatureId Id,
    string Owner,
    bool Optional = false,
    string? Reason = null);

public sealed record RequiredModule(
    string ModuleName,
    string Owner,
    bool Optional = false,
    string? Reason = null);

public sealed record ModuleProfileDescriptor(
    string ModuleName,
    string ProfileName,
    IReadOnlyList<ProvidedFeature> Provides,
    IReadOnlyList<RequiredFeature> Requires,
    IReadOnlyList<RequiredModule> RequiredModules);
```

Names are illustrative. Choose final names that fit the existing style.

## Descriptor Integration

Add a descriptor feature for profile/capability metadata:

```csharp
public sealed record ModuleCompositionDescriptor(
    IReadOnlyList<ModuleProfileDescriptor> Profiles)
    : ModuleDescriptorFeature("composition.profiles");
```

Add builder/read helpers:

```csharp
ModuleDescriptor.Create(AuthModuleMetadata.Name)
    .WithProfile(AuthProfiles.Global)
    .WithProfile(AuthProfiles.ScopeAware)
    .Build();

descriptor.GetProfiles();
descriptor.GetRequiredFeatures(profileName);
descriptor.GetProvidedFeatures(profileName);
```

This metadata is for docs, tests, scaffolding, and validation. It must not auto-register modules or services.

## Runtime Registration Model

Runtime composition should register selected profiles and provided features explicitly.

Possible API shape:

```csharp
builder.AddCompositionFeatures();

builder.ProvideFeature(CompositionFeatures.Persistence);
builder.ProvideFeature(TenancyFeatures.Context);

builder.AddAuthModule(AuthProfile.Global("global"));
builder.AddCatalogModule(CatalogProfile.WithDurableEvents());

builder.ValidateModuleComposition();
```

Or module-oriented:

```csharp
builder.AddAuthModule(AuthProfile.ScopeAware());
builder.AddTenancyModule();

builder.Services.AddModuleCompositionValidation();
```

The concrete API can differ, but it must support:

- adding a selected module profile;
- declaring provided features;
- declaring required features;
- declaring required modules;
- validating after all intended composition calls have run;
- producing deterministic error messages.

## Validation Requirements

Validation should fail with clear messages such as:

```text
Module 'auth' profile 'scope-aware' requires feature 'scoping.context',
but no composed module or adapter provides it.
Register scoping infrastructure plus a tenant bridge, or use AuthProfile.Global("global").
```

Rules:

- Every selected required feature must be provided by the host, a shared adapter, or another selected module profile.
- Every selected required module must be selected in the same composition root.
- Duplicate provided features are allowed only when they are compatible or explicitly marked multi-provider.
- Conflicting features must fail.
- Missing optional features should not fail, but should be visible in diagnostics if useful.
- Validation should run in API, admin API, admin CLI, worker, and test hosts when composed.
- Validation must not require starting hosted services.
- Validation must be unit-testable without building a full web host.

## Module-Owned Profiles

Modules own the behavior behind their profiles.

Auth example:

```csharp
public sealed record AuthProfile
{
    public static AuthProfile Global(string scopeId) => ...;
    public static AuthProfile ScopeAware() => ...;
}
```

Possible behavior:

```text
AuthProfile.Global("global")
  -> no scoping.context requirement
  -> stores "global" in existing TenantId/scope column
  -> tokens/admin reads use the same global scope
  -> still composes baseline Gma.Framework.Infrastructure so IScopeContext resolves to the configured default scope

AuthProfile.ScopeAware()
  -> requires scoping.context
  -> stores resolved scope id
  -> fails when scope id is missing
```

The module should decide whether a capability is:

- required;
- optional with fallback;
- unsupported and should fail;
- provided to other modules.

Shared composition code should not decide Auth semantics.

## Cross-Boundary Adapter Packages

Use small adapter packages when one feature adds behavior on top of another feature.

Existing examples:

```text
Gma.Framework.Caching.Cqrs
  Caching + CQRS post-commit behavior

Gma.Framework.Notifications.Cqrs
  Notifications + CQRS post-commit behavior

Gma.Framework.Tenancy.Api.Serilog
  Tenancy + HTTP request logging enrichment

Gma.Framework.Tenancy.Caching
  Tenancy + cache scope value resolution

Gma.Framework.Tenancy.Cqrs
  Tenancy + CQRS logging scope enrichment

Gma.Framework.Tenancy.Messaging
  tenant integration-event base type
  tenant messaging composition feature ids

Gma.Framework.Tenancy.Messaging.Infrastructure
  generic messaging scope resolver
  tenant context setup for consumers

Gma.Framework.Tenancy.Tasks
  tenant context setup for task workers

Gma.Framework.Messaging.Nats.Aspire
  Messaging + Aspire/NATS connection composition

Gma.Framework.ProjectionRebuild.Tasks
  Projection rebuild + TaskRuntime progress/control bridge
```

Future examples:

```text

Gma.Framework.Tenancy.Outbox
  optional tenant metadata storage/mapping for outbox rows

Gma.Framework.AccessControl.Administration
  optional adapter from admin RBAC decisions to resource-policy decisions
```

These packages should depend only on the smallest contracts needed from each side.

## Tenancy And Outbox Decoupling Direction

Shared messaging/outbox code is tenant-neutral in source: base integration events, envelopes, outbox records, and inbox records expose generic message scope metadata. Tenant-owned event contracts live in `Gma.Framework.Tenancy.Messaging`; runtime scope/context behavior lives in `Gma.Framework.Tenancy.Messaging.Infrastructure`.

Shared caching runtime is tenant-neutral in source: `Gma.Framework.Caching.Infrastructure` formats logical cache scopes through a generic `ICacheScopeValueResolver`. Tenant-owned cache keys require the `caching.scope-context` feature provided by `Gma.Framework.Tenancy.Caching`.

Shared task worker runtime is tenant-neutral in source: `Gma.Framework.Tasks.Infrastructure` prepares execution context through generic `ITaskExecutionContextContributor` instances. Tenant-scoped task payloads require the `tasks.scope-context` feature provided by `Gma.Framework.Tenancy.Tasks`.

The implemented shape is:

```text
Base event/envelope:
  event id
  event name
  version
  occurred at
  payload
  optional generic scope id

Tenancy adapter:
  ScopedIntegrationEvent base type
  scope id validation
  generic scope resolver
  tenant context setup

Outbox base storage:
  message id
  subject
  event type/version
  payload
  lifecycle fields

Compatibility storage:
  ScopeId currently maps to the existing TenantId column
```

Do not remove scope ids from real modules casually. For many reusable modules, especially Auth, keeping one stable schema with an explicit global/default scope value is better than generating scope-free and scope-aware migrations.

The first slice intentionally avoided a broad migration or physical column rename.

## Auth Proof Slice

Auth is the best first proof because it is both reusable and currently tenant-shaped.

Implement only if feasible within the first slice:

- add an `AuthProfile` object;
- support a global/default scope id profile;
- support a scope-aware profile requiring `scoping.context`;
- keep existing schema shape;
- use a non-empty configured scope value such as `global`;
- update public/admin Auth composition calls;
- add startup validation for invalid profile combinations;
- add tests proving global profile does not require Tenancy module;
- add tests proving the scope-aware profile fails without a scope context provider.

If the Auth change is too large, create a smaller fake/sample module profile in tests first and leave Auth as the second slice.

## Feature Naming

Feature ids should be lowercase kebab/dotted stable identifiers.

Suggested shape:

```text
tenancy.context
messaging.outbox
messaging.nats-publishing
messaging.nats-consumers
tasks.worker
tasks.scheduler
caching.application
caching.redis
notifications.live
notifications.history
administration.rbac
auth.members
auth.sessions
```

Rules:

- shared packages own shared feature ids;
- modules own module-specific feature ids;
- avoid tenant ids, user ids, resource ids, or environment names in feature ids;
- avoid using feature ids as permissions;
- avoid using feature ids as runtime feature flags for product behavior.

## Diagnostics

Add a small composition report suitable for logs/tests:

```text
Selected modules:
  auth profile=global
  catalog profile=default

Provided features:
  auth.members by auth/global
  messaging.outbox by Gma.Framework.Messaging.Infrastructure

Required features:
  catalog durable-events requires messaging.outbox satisfied
```

This report should be safe to log. Do not include secrets, tenant ids, user ids, or connection strings.

## Testing Requirements

Add focused tests:

- feature id normalization rejects empty, whitespace, control characters, and invalid separators;
- duplicate required/provided feature handling is deterministic;
- selected profile with missing required feature fails;
- selected profile with missing required module fails;
- optional missing feature does not fail;
- incompatible duplicate providers fail when marked exclusive;
- descriptors expose composition profile metadata;
- metadata does not auto-register services;
- architecture tests keep base packages free from unrelated optional package references;
- Auth global profile can compose without Tenancy module, if Auth is included in the first proof;
- Auth scope-aware profile fails without a scope context provider, if Auth is included in the first proof.

## Documentation Requirements

Update:

- `docs/README.md`;
- `src/Framework/docs/architecture/module-system.md`;
- `src/Framework/docs/architecture/metadata-attribute-pipeline-task.md`;
- `src/Framework/docs/guidelines/development-guidelines.md`;
- `src/Framework/docs/templates/module.md`;
- `src/Framework/docs/architecture/production-readiness-backlog.md`;
- relevant module docs for any proof module.

Docs must explain:

- what a composition feature is;
- how a module declares profiles;
- how a host selects a profile;
- when to use required features versus optional fallback behavior;
- when to create a cross-boundary adapter package;
- why metadata still does not perform runtime discovery or service registration.

## Acceptance Criteria

- A generic shared composition feature model exists.
- Hosts/modules/adapters can declare provided and required features.
- Selected module profiles can be registered explicitly.
- Composition validation fails fast with useful errors.
- The production-readiness backlog links this task at the top.
- At least one module or test fixture proves a missing required feature fails.
- At least one optional feature/fallback path is documented or tested.
- Architecture tests preserve explicit composition and package boundaries.
- Docs describe how to add a new module profile and a new cross-boundary adapter package.

## Implementation Slices

1. Add this task doc and backlog entry.
2. Add shared composition feature primitives.
3. Add selected-profile and provided-feature registration.
4. Add composition validation and deterministic diagnostics.
5. Add descriptor metadata integration.
6. Add tests with a small fake/test module profile.
7. Add Auth global versus scope-aware profile as the first real proof, if feasible.
8. Document cross-boundary adapter package rules.
9. Plan remaining tenancy/caching/tasks decoupling as follow-up bridge packages.

## Open Questions For Implementation

- Should the package be named `Gma.Framework.ModuleComposition`, `Gma.Framework.Composition`, or stay under `Gma.Framework.Modules`?
- Should validation run through an explicit `builder.ValidateModuleComposition()` call, an `IHostedService`, options validation, or all of these for different host types?
- Should provided features be multi-provider by default, exclusive by default, or require a per-feature policy?
- Should module profile descriptors live only in contracts metadata, or should runtime selected profiles be separate objects?
- Auth now uses `ScopeId` language in reusable code while tenant-facing adapters keep tenant terminology at boundaries.
- Which cross-boundary package should be split next after tenant messaging: tenancy plus caching, tenancy plus tasks, or tenant-aware CQRS logging?

# Testing Guidelines

## Test Projects

```text
gma/framework/tests/Gma.Framework.Tests
gma/modules/administration/tests/Gma.Modules.Administration.Tests
gma/modules/auth/tests/Gma.Modules.Auth.Tests
gma/modules/notifications/tests/Gma.Modules.Notifications.Tests
src/Modules/Catalog/tests/Catalog.Tests
src/Modules/Ordering/tests/Ordering.Tests
tests/Architecture.Tests
tests/Integration.Tests
tests/ServiceDefaults.Tests
```

Focused framework, reusable-module, and example-module unit tests live under the source root that owns them. In a package repository the same paths are rooted at that repository, for example `tests/Gma.Framework.Tests` or `tests/Gma.Modules.Auth.Tests`. Root-level `tests/` in the skeleton is for composition tests, cross-module integration tests, architecture coverage, and host/service-default tests.

Test project names:

```text
Gma.Modules.Administration.Tests
Architecture.Tests
Gma.Modules.Auth.Tests
Catalog.Tests
Integration.Tests
Ordering.Tests
ServiceDefaults.Tests
Gma.Framework.Tests
Gma.Modules.Notifications.Tests
```

Every test project must declare:

- `<IsTestProject>true</IsTestProject>` so `dotnet test` discovers it reliably.
- `<IsPackable>false</IsPackable>` so test assemblies are never packaged accidentally.
- `Microsoft.NET.Test.Sdk`, `xunit`, and `xunit.runner.visualstudio`.
- `<PrivateAssets>all</PrivateAssets>` on `xunit.runner.visualstudio` so the runner stays test-local.

## Categories

Use xUnit traits:

```csharp
[Trait("Category", "Unit")]
[Trait("Category", "Architecture")]
[Trait("Category", "Integration")]
[Trait("Category", "Docker")]
```

Docker-backed tests should include both:

```csharp
[Trait("Category", "Integration")]
[Trait("Category", "Docker")]
```

Every test source file that contains `[Fact]`, `[Theory]`, or `[DockerFact]` must declare its expected category somewhere in the file:

- `Architecture.Tests` files use `Architecture`.
- `Integration.Tests` files use `Integration`.
- all other test projects use `Unit`.
- files with `[DockerFact]` also declare `Docker`.

## Folder Layout

Test source files live under one intent folder below the test project root. Keep the folder name about why the test exists, not about incidental implementation detail.

Use these defaults:

- `Support/` for fixtures, test applications, helper attributes, shared test collections, and catalogs.
- Feature module unit tests mirror the module layer: `Application/`, `Contracts/`, `Domain/`, `Persistence/`, `Security/`, or a focused capability such as `Projections/`.
- `Gma.Framework.Tests` groups by shared capability: `Administration/`, `Api/`, `Caching/`, `Cqrs/`, `Domain/`, `Infrastructure/`, `Messaging/`, `Modules/`, `Numerics/`, `Observability/`, `Results/`, `Security/`.
- `Architecture.Tests` uses `Boundaries/`, `DeveloperExperience/`, `Modules/`, and `Support/`.
- `Integration.Tests` groups by runtime surface or adapter: `AdminApi/`, `AdminCli/`, `Auth/`, `Caching/`, `Messaging/`, `Observability/`, `Persistence/`, and `Support/`.

Avoid dropping new test files directly in the test project root. Architecture tests enforce the intent-folder rule so the suite stays browsable as optional modules and adapters grow.

## Commands

Fast tests exclude Docker-backed tests through `Category!=Docker`:

```powershell
.\eng\test-fast.ps1 -NoBuild
```

Docker tests run only `Category=Docker`, set `GMA_REQUIRE_DOCKER_TESTS=true`, and restore the previous environment value when finished:

```powershell
.\eng\test-docker.ps1 -NoBuild
```

`eng/verify.ps1` intentionally runs the fast test script only after restore, build, and the provider migration drift check. Run `eng/test-docker.ps1` separately when the current slice touches containers, Redis, SQL Server, PostgreSQL, NATS, or other Docker-backed infrastructure.

From the framework package root, validate the framework package through its focused solution:

```powershell
dotnet test Gma.Framework.slnx --no-build --logger "console;verbosity=minimal"
```

From the skeleton composition root, validate the all-up host, reusable modules, examples, and composition tests through the skeleton solution:

```powershell
dotnet test GMA-Skeleton.slnx --no-build --logger "console;verbosity=minimal"
```

If .NET 10 is installed outside `PATH`, set `GMA_DOTNET` and use the `eng/*.ps1` scripts so local tooling still resolves the pinned SDK intentionally.

## Unit Tests

Use unit tests for:

- aggregate invariants;
- value object rules;
- result behavior;
- domain event collection;
- command validation;
- unit-of-work behavior that can run in memory.

## Architecture Tests

Architecture tests should protect:

- module dependency boundaries;
- domain independence from EF Core and ASP.NET Core;
- application independence from persistence and API;
- explicit module catalog coverage in `ArchitectureCatalog`;
- solution project membership;
- namespace alignment;
- package-reference boundaries for CLI and infrastructure adapters;
- test naming;
- test folder organization;
- no `EnsureCreated` in integration tests.

When a module adds or removes compiled projects, update `tests/Architecture.Tests/Support/ArchitectureCatalog.cs`. Keep this catalog explicit and test-only; it is not a runtime registration mechanism.

## Integration Tests

Use integration tests for:

- endpoint lifecycle behavior;
- provider-specific persistence behavior;
- migration-backed schema behavior;
- tenant isolation;
- outbox publishing;
- infrastructure adapters.
- admin CLI flows that cross RBAC, tenancy, module application handlers, and persistence.

Tenant convention tests should prove both read and write isolation:

- the shared EF helper applies `ScopeFilter` dynamically per `DbContext` scope, not from a stale first-built model;
- `IScopedEntity` mapped entities have a required `ScopeId` with `ScopeIds.MaxLength`;
- `ScopeAwareDbContext<TContext>` rejects invalid, unnormalized, and mismatched scope ids before commit;
- global or runtime/control-plane records are intentionally exempted and documented.

Integration tests should use migrations:

```csharp
await dbContext.Database.MigrateAsync();
```

Do not use:

```csharp
await dbContext.Database.EnsureCreatedAsync();
```

## Docker Tests

`DockerFact` skips locally when Docker is unavailable unless:

```powershell
$env:GMA_REQUIRE_DOCKER_TESTS = 'true'
```

`eng/test-docker.ps1` sets this automatically.

## Test Naming

Class names:

```text
<Behavior>Tests
<Feature>IntegrationTests
```

Method names should read as behavior statements:

```text
Create_raises_registered_domain_event
Members_and_sessions_are_isolated_by_tenant
Admin_cli_bootstraps_rbac_and_manages_auth_members_against_sql_server_and_postgre_sql
```

## Test Data

- Keep tenant ids explicit.
- Use unique database names or isolated containers.
- Avoid relying on test order.
- Prefer deterministic timestamps for domain tests.
- Tests that redirect process-wide state such as `Console.Out`, `Console.Error`, or `Console.In` must use a dedicated xUnit collection so they do not race with each other. `Gma.Framework.Tests` uses `ConsoleTestIsolation` for this.

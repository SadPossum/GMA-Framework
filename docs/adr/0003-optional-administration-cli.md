# ADR 0003: Optional Administration CLI

## Status

Accepted

## Context

The skeleton needs administration features without turning every API deployment into an admin API deployment. Admin operations also need persisted RBAC, audit, tenant scope, and a first real domain feature for Auth user management.

The project goal is still a small optional-module modular monolith. Admin support should be removable when a project does not need it.

## Decision

Add administration as an optional CLI-first capability:

- `Gma.Framework.Administration` contains generic contracts.
- `Gma.Framework.Administration.Cli` contains `System.CommandLine` integration.
- `Host.AdminCli` is a separate packable .NET tool named `gma-admin`.
- `Administration` owns persisted RBAC and audit in the `admin` schema.
- Feature modules expose CLI front doors through `<Module>.AdminCli`.
- Auth exposes user administration through `Gma.Modules.Auth.AdminCli`.

`Host.Api` does not register admin modules in this milestone.

## Consequences

Good:

- Public API composition stays simple.
- Admin dependencies are isolated to explicit admin projects.
- RBAC/audit persistence is optional.
- Feature admin commands reuse the same CQRS/domain/persistence paths as public APIs.
- Architecture tests can enforce `System.CommandLine` isolation.

Tradeoffs:

- A project that wants admin behavior must compose and deploy `Host.AdminCli`.
- CLI-first workflows are less discoverable than a UI.
- Audit and mutation commits are separate; audit failures are surfaced but do not roll back already committed mutations.

## Follow-Ups

- Admin HTTP endpoints were added through ADR 0004 as a separate host/module decision.
- Add richer audit querying when a real operator workflow needs it.
- Add command docs for any new `<Module>.AdminCli` project in the same change that introduces it.

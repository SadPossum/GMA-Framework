# Composition Tooling

Framework `eng/` owns product-neutral tooling that composition repositories can invoke after the framework source checkout is mounted.

## Ownership

The framework owns implementations for:

- module scaffolding;
- provider migration creation and drift checks;
- deterministic `.slnx` synchronization;
- source-package validation;
- configured submodule head validation;
- reproducible source-set manifests.

Composition repositories own thin wrappers that provide their repository root, solution name, project prefix, and host paths. They also own the pre-mount submodule initializer because framework tooling cannot run before `gma/framework` exists.

## Naming

`new-module.ps1` keeps unprefixed projects as its compatibility default. A product can pass a stable prefix such as `Acme.Modules` to generate `Acme.Modules.Billing.Contracts`, `Acme.Modules.Billing.Domain`, and the other selected project roles directly. Project names and namespaces remain aligned; the scaffolder does not set `RootNamespace` or `AssemblyName` overrides.

## Host Registration

Public API registration is optional. A composition wrapper may pass its API project, `Program.cs`, and registration marker. The scaffolder validates all three before changing the host. Other hosts remain explicit composition decisions.

## Safety

- migration targets are discovered from matching `.Persistence` and provider-migration project suffixes;
- solution output is generated through XML APIs and supports a non-writing `-Check` mode;
- submodule validation follows each branch declared in `.gitmodules`, with an optional expected-branch policy;
- source-set export records exact commits, configured branches, SDK identity, package-catalog hash, and dirty state;
- no runtime project references these scripts.

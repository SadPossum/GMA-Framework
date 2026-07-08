# Documentation Guidelines

Docs are part of the architecture. If a module or boundary changes, update the docs in the same change.

## Format

Use plain Markdown.

The docs should work in:

- GitHub;
- Visual Studio;
- Rider;
- VS Code;
- Obsidian.

Do not require Obsidian plugins or `.obsidian` settings.

## Structure

Documentation is source-owned. In the source-first layout, each repository keeps its docs beside the source it owns:

```text
GMA-Skeleton/
  docs/
    README.md
    getting-started/
    architecture/
    examples/

GMA-Framework/
  docs/
    README.md
    architecture/
    guidelines/
    templates/
    adr/

GMA-Module-Auth/
  docs/
    README.md
```

In a skeleton checkout, those source repositories are mounted under `gma/`, for example:

```text
gma/framework/docs/
  README.md
  architecture/
  guidelines/
  templates/
  adr/

gma/modules/<alias>/docs/
  README.md
```

Root `docs/` describes the skeleton/template repository and links out to source-owned framework and module documentation. Framework docs describe reusable framework packages, cross-cutting architecture, templates, guidelines, and ADRs. Module docs describe that module only.

Skeleton docs may mention mounted local paths such as `gma/framework/docs/README.md`, but Markdown links to reusable docs should point at the owning source repository on GitHub. GitHub cannot reliably render deep file links through a skeleton submodule path such as `gma/modules/tenancy/docs/README.md`.

## What to Document

Document when a change affects:

- module public API;
- module behavior;
- persistence schema;
- configuration;
- deployment;
- integration events;
- tenant behavior;
- test strategy;
- developer workflow.

## Module Docs

Each reusable module should have `docs/README.md` at the module repository root. In a skeleton checkout, that same file appears under `gma/modules/<alias>/docs/README.md`.

Use [../templates/module.md](../templates/module.md).

Minimum sections:

- purpose;
- projects;
- public contracts;
- endpoints;
- domain model;
- persistence;
- integration events;
- tests;
- extension points.

## ADRs

Use ADRs for decisions that are hard to reverse or likely to be questioned later.

Examples:

- adopting a tenancy strategy;
- introducing a new infrastructure adapter;
- changing module boundaries;
- replacing Auth implementation;
- changing event subject format.

Use [../templates/adr.md](../templates/adr.md).

## Writing Style

- Prefer present tense.
- Be specific about paths and commands.
- Keep claims tied to the current repo.
- Avoid marketing language.
- Avoid copying implementation details that will drift quickly unless the detail matters.
- Link to source files when useful.

## Docs Review Checklist

- Does the doc match current code?
- Are commands runnable from repo root?
- Are config keys spelled exactly?
- Are module boundaries clear?
- Is the page linked from the owning docs index?
- If the page belongs to a reusable framework or module package, is it under that package repository's root `docs/`?
- If a skeleton doc links to reusable docs, does the link target the owning source repository rather than a deep `gma/...` submodule path?
- Is a template needed for repeating this doc shape?

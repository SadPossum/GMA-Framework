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

Documentation is source-owned. In the current monorepo staging layout, source-owned docs live under the staged package source roots:

```text
docs/
  README.md
  getting-started/
  architecture/
  examples/

src/Framework/docs/
  README.md
  architecture/
  guidelines/
  templates/
  adr/

src/Modules/<Module>/docs/
  README.md
```

Root `docs/` describes the skeleton/template repository and links out to source-owned framework and module documentation. Framework docs describe reusable framework packages, cross-cutting architecture, templates, guidelines, and ADRs. Module docs describe that module only.

When a framework or reusable module is extracted into its own repository, move its docs to that repository's root-level `docs/` folder. Do not preserve monorepo parent folders such as `src/Framework/docs/` or `src/Modules/Auth/docs/` inside the standalone repository.

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

Each reusable module should have `src/Modules/<Module>/docs/README.md` while it is staged in this monorepo. In a standalone module repository, the same content should live at `docs/README.md`.

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
- If the page belongs to a reusable framework or module package in this monorepo, is it outside skeleton root `docs/`?
- If the page belongs to an extracted framework or module repository, is it under that repository's root `docs/`?
- Is a template needed for repeating this doc shape?

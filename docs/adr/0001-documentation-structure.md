# ADR 0001: Documentation Structure

## Status

Accepted

## Date

2026-06-15

## Context

The project is a modular monolith skeleton intended to be reused across future projects. It needs documentation that helps with onboarding, module authoring, naming, deployment, and architectural consistency.

The docs should be easy to read in common developer tools and should not depend on a proprietary or local-only documentation system.

## Decision

Use plain Markdown, with documentation owned by the repository or source package it describes.

In the current monorepo staging layout, reusable package docs live under the staged package source roots:

```text
docs/
  README.md
  getting-started/
  architecture/

src/Framework/docs/
  README.md
  architecture/
  guidelines/
  templates/
  adr/

src/Modules/<Module>/docs/
  README.md
```

The root `docs/` tree is for the skeleton/template repository: local setup, host composition, example workflows, and source-split planning. Framework behavior, reusable templates, ADRs, and framework guidelines live under `src/Framework/docs/`. Reusable module behavior lives under `src/Modules/<Module>/docs/`.

After the framework and reusable modules become independent source repositories, each repository should move its owned docs to its own repository root:

```text
gma-framework/
  docs/
  src/
  tests/
  eng/

gma-module-auth/
  docs/
  src/
  tests/
  eng/
```

The extracted repositories should not keep monorepo parent folders such as `src/Framework/docs/` or `src/Modules/Auth/docs/` inside themselves. The package repo root is already the ownership boundary.

The docs are compatible with Obsidian, but the repo does not commit `.obsidian` configuration or plugin requirements.

## Consequences

Positive:

- works in GitHub, IDEs, and Obsidian;
- keeps docs versioned with code;
- keeps framework and module docs beside their source while in monorepo staging;
- gives independent framework/module repositories normal root-level docs after extraction;
- supports repeatable templates for new modules and decisions.

Negative:

- diagrams are limited to plain Markdown or Mermaid;
- no generated documentation site until one is needed.

## Alternatives Considered

- Obsidian-specific vault: good personal navigation, but adds editor-specific state to the repo.
- Static docs site: useful later, but too much infrastructure for the current skeleton.
- Single huge README: simple at first, but harder to maintain as modules grow.

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

In the current source-first layout, reusable package docs live at the owning repository root:

```text
gma-skeleton/
  docs/
    README.md
    getting-started/
    architecture/
    examples/

gma-framework/
  README.md
  docs/
  src/
  tests/
  eng/

gma-module-auth/
  README.md
  docs/
  src/
  tests/
  eng/
```

When a skeleton checkout mounts the source repositories as submodules, those same docs appear under `gma/framework/docs/` and `gma/modules/<alias>/docs/`.

The root `docs/` tree is for the skeleton/template repository: local setup, host composition, example workflows, and source-split planning. Framework behavior, reusable templates, ADRs, and framework guidelines live under the framework repository's `docs/`. Reusable module behavior lives under that module repository's `docs/`.

Reusable repositories should not keep monorepo parent folders such as `src/Framework/docs/` or `src/Modules/Auth/docs/` inside themselves. The package repo root is already the ownership boundary.

Skeleton docs may mention mounted local paths such as `gma/modules/auth/docs/README.md`, but Markdown links from skeleton docs to reusable docs should target the owning source repository on GitHub. GitHub cannot reliably render deep file links through a skeleton submodule path.

The docs are compatible with Obsidian, but the repo does not commit `.obsidian` configuration or plugin requirements.

## Consequences

Positive:

- works in GitHub, IDEs, and Obsidian;
- keeps docs versioned with code;
- keeps framework and module docs beside their source;
- gives independent framework/module repositories normal root-level docs;
- supports repeatable templates for new modules and decisions.

Negative:

- diagrams are limited to plain Markdown or Mermaid;
- no generated documentation site until one is needed.

## Alternatives Considered

- Obsidian-specific vault: good personal navigation, but adds editor-specific state to the repo.
- Static docs site: useful later, but too much infrastructure for the current skeleton.
- Single huge README: simple at first, but harder to maintain as modules grow.

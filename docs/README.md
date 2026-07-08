# GMA Framework Documentation

This folder documents reusable GMA framework packages, cross-cutting extension points, source-owned ADRs, and framework-level development conventions.

The skeleton/composition repository links here from [root documentation](https://github.com/SadPossum/gma-skeleton/blob/dev/docs/README.md). Application-template guidance stays in the skeleton `docs/`; reusable framework behavior stays here.

## Architecture

- [Architecture Overview](architecture/overview.md)
- [Module System](architecture/module-system.md)
- [Module Composition Features And Profiles Task](architecture/module-composition-features-task.md)
- [CQRS and Domain Events](architecture/cqrs-and-domain-events.md)
- [Persistence and Tenancy](architecture/persistence-and-tenancy.md)
- [Metadata Attribute Pipeline Refactor Task](architecture/metadata-attribute-pipeline-task.md)
- [Messaging and Outbox](architecture/messaging-and-outbox.md)
- [Messaging Consumers](architecture/messaging-consumers.md)
- [Background Worker Host Task](architecture/background-worker-host-task.md)
- [File Management](architecture/file-management.md)
- [Tasks and Daemons](architecture/tasks-and-daemons.md)
- [Notifications and Streaming](architecture/notifications-and-streaming.md)
- [Observability](architecture/observability.md)
- [Caching](architecture/caching.md)
- [Administration](architecture/administration.md)
- [Shared Access Policy Foundation](architecture/shared-access-policy-foundation.md)
- [Scoped Resource Access](architecture/scoped-resource-access.md)
- [Projection Rebuild Tasks](architecture/projection-rebuild-tasks.md)
- [Tenant Model Conventions Task](architecture/tenant-model-conventions-task.md)

## Implementation Notes

- [Module Descriptor Builder Refactor Notes](architecture/module-descriptor-builder-refactor-notes.md)
- [Metadata Attributes Refactor Notes](architecture/metadata-attributes-refactor-notes.md)
- [Shared Dependency Boundary Refactor Notes](architecture/shared-dependency-boundary-refactor-notes.md)
- [Shared Access Policy Implementation Notes](architecture/shared-access-policy-implementation-notes.md)
- [Projection Rebuild Implementation Notes](architecture/projection-rebuild-implementation-notes.md)
- [Notifications and Streaming Implementation Notes](architecture/notifications-streaming-implementation-notes.md)
- [Semantic Enum Refactor Notes](architecture/semantic-enum-refactor-notes.md)
- [Tenant Model Conventions Implementation Notes](architecture/tenant-model-conventions-implementation-notes.md)
- [Production Readiness Backlog](architecture/production-readiness-backlog.md)
- [Architecture Hardening Notes](architecture/audit-hardening-notes.md)
- [Architecture Audit Follow-Up Notes](architecture/audit-follow-up-notes.md)

## Guidelines

- [Naming Conventions](guidelines/naming-conventions.md)
- [Development Guidelines](guidelines/development-guidelines.md)
- [Testing Guidelines](guidelines/testing-guidelines.md)
- [Deployment Guidelines](guidelines/deployment-guidelines.md)
- [Documentation Guidelines](guidelines/documentation-guidelines.md)

## Templates

- [Module Documentation Template](templates/module.md)
- [ADR Template](templates/adr.md)
- [Integration Event Template](templates/integration-event.md)
- [Endpoint Template](templates/endpoint.md)

## ADRs

- [0001 Documentation Structure](adr/0001-documentation-structure.md)
- [0002 Explicit Optional Caching](adr/0002-explicit-optional-caching.md)
- [0003 Optional Administration CLI](adr/0003-optional-administration-cli.md)
- [0004 Optional Administration API](adr/0004-optional-administration-api.md)
- [0005 NATS Consumers and Cross-Module Data Ownership](adr/0005-nats-consumers-and-cross-module-data-ownership.md)
- [0006 Constrained Application Service Registration](adr/0006-constrained-application-service-registration.md)
- [0007 Shared CQRS Validation Contracts](adr/0007-shared-cqrs-validation-contracts.md)
- [0008 Optional Tasks and Daemons Foundation](adr/0008-optional-tasks-and-daemons-foundation.md)
- [0009 Configurable Application Identity](adr/0009-configurable-application-identity.md)
- [0010 Projection Rebuild Tasks](adr/0010-projection-rebuild-tasks.md)
- [0011 Optional User Notifications and Streaming](adr/0011-optional-user-notifications-and-streaming.md)
- [0012 Optional Notifications Module](adr/0012-optional-notifications-module.md)

## Documentation Rules

- Keep framework docs close to framework source.
- Link to skeleton docs for application composition, local setup, and example-module workflows.
- Link to module docs for reusable module behavior.
- Update docs in the same change that updates framework behavior.

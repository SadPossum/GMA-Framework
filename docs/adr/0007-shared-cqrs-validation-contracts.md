# ADR 0007: Shared CQRS Validation Contracts

## Status

Accepted.

## Context

The backlog asked whether the skeleton should switch validation to FluentValidation.

The current validation model is intentionally small:

- request-shape checks implement `ICommandValidator<TCommand>` or `IQueryValidator<TQuery>`;
- command and query validation runs in the shared CQRS pipeline;
- API, Admin API, CLI, and tests exercise the same application validators;
- deeper business invariants stay in aggregates and domain services;
- options/configuration validation uses `IValidateOptions<TOptions>`.

Most current request validators are short and do not need a richer rule DSL. The value of switching would be consistency with a popular library and access to FluentValidation's mature rule model.

The cost is another default dependency and a second validation abstraction unless the project replaces every current validator consistently. FluentValidation's own ASP.NET guidance recommends manual validation over automatic ASP.NET validation for modern async-capable scenarios, and its DI auto-registration uses reflection scanning that would overlap with the project's constrained application registration rule.

## Decision

Do not switch the skeleton to FluentValidation by default.

Keep the shared CQRS validator contracts as the default validation boundary:

```csharp
internal sealed class CreateCatalogItemCommandValidator : ICommandValidator<CreateCatalogItemCommand>
{
    public IEnumerable<string> Validate(CreateCatalogItemCommand command)
    {
        // request-shape checks only
    }
}
```

Modules may introduce a FluentValidation adapter later only through a separate ADR or module-specific decision that proves the benefit. That adapter must still feed the shared CQRS validation result shape so API, Admin API, CLI, and tests stay aligned.

## Consequences

The default skeleton stays small, dependency-light, and easy to follow. Validators remain plain C# classes and can be registered by the constrained application assembly helper.

The tradeoff is less declarative validation syntax. If future modules accumulate complex nested object validation, localization, conditional rule sets, or reusable property validators, revisit this decision with a real module use case and an adapter design.

## Guardrails

- Default production projects do not reference `FluentValidation`, `FluentValidation.AspNetCore`, or `FluentValidation.DependencyInjectionExtensions`.
- Module API front doors do not own request validation rules.
- Request-shape validation stays in application validators.
- Business invariants stay in aggregates/domain services.
- Configuration validation stays in options validators.

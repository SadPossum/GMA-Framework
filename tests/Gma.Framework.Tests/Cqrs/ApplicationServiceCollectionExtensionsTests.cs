namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Cqrs;
using Gma.Framework.Application.Composition;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Domain;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void Add_application_services_from_assembly_rejects_null_arguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            ApplicationServiceCollectionExtensions.AddApplicationServicesFromAssembly(
                null!,
                typeof(ApplicationServiceCollectionExtensionsTests).Assembly));
        Assert.Throws<ArgumentNullException>(() => services.AddApplicationServicesFromAssembly(null!));
    }

    [Fact]
    public void Add_application_services_from_assembly_registers_supported_services_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddApplicationServicesFromAssembly(typeof(ApplicationRegistrationCommand).Assembly);

        AssertScoped<ICommandHandler<ApplicationRegistrationCommand, Unit>, ApplicationRegistrationCommandHandler>(
            services);
        AssertScoped<IQueryHandler<ApplicationRegistrationQuery, string>, ApplicationRegistrationQueryHandler>(
            services);
        AssertScoped<ICommandValidator<ApplicationRegistrationCommand>, ApplicationRegistrationCommandValidator>(
            services);
        AssertScoped<IQueryValidator<ApplicationRegistrationQuery>, ApplicationRegistrationQueryValidator>(
            services);
        AssertScoped<IDomainEventHandler<ApplicationRegistrationDomainEvent>, ApplicationRegistrationDomainEventHandler>(
            services);
    }

    [Fact]
    public void Add_application_services_from_assembly_is_repeat_safe()
    {
        var services = new ServiceCollection();

        services.AddApplicationServicesFromAssembly(typeof(ApplicationRegistrationCommand).Assembly);
        services.AddApplicationServicesFromAssembly(typeof(ApplicationRegistrationCommand).Assembly);

        ServiceDescriptor[] descriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(ICommandHandler<ApplicationRegistrationCommand, Unit>) &&
                descriptor.ImplementationType == typeof(ApplicationRegistrationCommandHandler))
            .ToArray();

        Assert.Single(descriptors);
    }

    [Fact]
    public void Add_application_services_from_assembly_ignores_unsupported_or_non_concrete_types()
    {
        var services = new ServiceCollection();

        services.AddApplicationServicesFromAssembly(typeof(ApplicationRegistrationCommand).Assembly);

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ImplementationType == typeof(AbstractApplicationRegistrationCommandHandler));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ImplementationType == typeof(OpenApplicationRegistrationValidator<>));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ImplementationType == typeof(ApplicationRegistrationIntegrationEventHandler));
    }

    [Fact]
    public void Registered_application_services_can_be_resolved_by_the_default_container()
    {
        var services = new ServiceCollection();
        services.AddApplicationServicesFromAssembly(typeof(ApplicationRegistrationCommand).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<ApplicationRegistrationCommandHandler>(
            provider.GetRequiredService<ICommandHandler<ApplicationRegistrationCommand, Unit>>());
        Assert.IsType<ApplicationRegistrationQueryHandler>(
            provider.GetRequiredService<IQueryHandler<ApplicationRegistrationQuery, string>>());
    }

    private static void AssertScoped<TService, TImplementation>(IServiceCollection services)
    {
        ServiceDescriptor descriptor = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private sealed record ApplicationRegistrationCommand : ICommand<Unit>;

    private sealed record ApplicationRegistrationQuery : IQuery<string>;

    private sealed record ApplicationRegistrationDomainEvent(
        Guid EventId,
        DateTimeOffset OccurredAtUtc) : IDomainEvent;

    private sealed record ApplicationRegistrationIntegrationEvent(
        Guid EventId,
        string ScopeId,
        DateTimeOffset OccurredAtUtc,
        string EventName,
        int Version) : IIntegrationEvent;

    private sealed class ApplicationRegistrationCommandHandler
        : ICommandHandler<ApplicationRegistrationCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            ApplicationRegistrationCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(Unit.Value));
    }

    private sealed class ApplicationRegistrationQueryHandler
        : IQueryHandler<ApplicationRegistrationQuery, string>
    {
        public Task<Result<string>> HandleAsync(
            ApplicationRegistrationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success("registered"));
    }

    private abstract class AbstractApplicationRegistrationCommandHandler
        : ICommandHandler<ApplicationRegistrationCommand, Unit>
    {
        public abstract Task<Result<Unit>> HandleAsync(
            ApplicationRegistrationCommand command,
            CancellationToken cancellationToken);
    }

    private sealed class ApplicationRegistrationCommandValidator
        : ICommandValidator<ApplicationRegistrationCommand>
    {
        public IEnumerable<string> Validate(ApplicationRegistrationCommand command) => [];
    }

    private sealed class ApplicationRegistrationQueryValidator
        : IQueryValidator<ApplicationRegistrationQuery>
    {
        public IEnumerable<string> Validate(ApplicationRegistrationQuery query) => [];
    }

    private sealed class OpenApplicationRegistrationValidator<TCommand> : ICommandValidator<TCommand>
        where TCommand : ICommand<Unit>
    {
        public IEnumerable<string> Validate(TCommand command) => [];
    }

    private sealed class ApplicationRegistrationDomainEventHandler
        : IDomainEventHandler<ApplicationRegistrationDomainEvent>
    {
        public Task HandleAsync(
            ApplicationRegistrationDomainEvent domainEvent,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ApplicationRegistrationIntegrationEventHandler
        : IIntegrationEventHandler<ApplicationRegistrationIntegrationEvent>
    {
        public Task HandleAsync(
            ApplicationRegistrationIntegrationEvent integrationEvent,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

namespace Gma.Framework.Application.Composition;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Gma.Framework.Application.Events;
using Gma.Framework.Cqrs;

public static class ApplicationServiceCollectionExtensions
{
    private static readonly Type[] SupportedServiceDefinitions =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(ICommandValidator<>),
        typeof(IQueryValidator<>),
        typeof(IDomainEventHandler<>)
    ];

    public static IServiceCollection AddApplicationServicesFromAssembly(
        this IServiceCollection services,
        Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        IEnumerable<ServiceDescriptor> descriptors = applicationAssembly
            .GetTypes()
            .Where(IsConcreteClosedType)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .SelectMany(CreateDescriptors);

        services.TryAddEnumerable(descriptors);

        return services;
    }

    private static bool IsConcreteClosedType(Type type) =>
        type is { IsClass: true, IsAbstract: false } &&
        !type.ContainsGenericParameters;

    private static IEnumerable<ServiceDescriptor> CreateDescriptors(Type implementationType) =>
        implementationType
            .GetInterfaces()
            .Where(IsSupportedServiceInterface)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(serviceType => ServiceDescriptor.Scoped(serviceType, implementationType));

    private static bool IsSupportedServiceInterface(Type serviceType) =>
        serviceType.IsGenericType &&
        SupportedServiceDefinitions.Contains(serviceType.GetGenericTypeDefinition());
}

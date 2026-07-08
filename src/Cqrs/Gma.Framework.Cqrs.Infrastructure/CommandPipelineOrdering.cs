namespace Gma.Framework.Cqrs.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Cqrs;

public static class CommandPipelineOrdering
{
    public static IServiceCollection MoveCommandUnitOfWorkBehaviorToEnd(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ServiceDescriptor[] unitOfWorkDescriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(ICommandPipelineBehavior<,>) &&
                descriptor.ImplementationType == typeof(CommandUnitOfWorkBehavior<,>))
            .ToArray();

        foreach (ServiceDescriptor descriptor in unitOfWorkDescriptors)
        {
            services.Remove(descriptor);
        }

        foreach (ServiceDescriptor descriptor in unitOfWorkDescriptors)
        {
            services.Add(descriptor);
        }

        return services;
    }
}

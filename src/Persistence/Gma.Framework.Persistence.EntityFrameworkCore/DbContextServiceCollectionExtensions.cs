namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class DbContextServiceCollectionExtensions
{
    public static IServiceCollection TryAddModuleDbContext<TDbContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsAction);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TDbContext)))
        {
            return services;
        }

        services.AddDbContext<TDbContext>(optionsAction);
        return services;
    }
}

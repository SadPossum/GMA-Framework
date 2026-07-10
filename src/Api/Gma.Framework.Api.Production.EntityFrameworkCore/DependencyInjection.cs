namespace Gma.Framework.Api.Production.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public static class DependencyInjection
{
    public static IServiceCollection AddGmaEntityFrameworkReadinessCheck<TDbContext>(
        this IServiceCollection services,
        string name)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddGmaReadinessCheck(name, async (provider, cancellationToken) =>
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            TDbContext dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

            try
            {
                bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
                return canConnect
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy($"Database for {typeof(TDbContext).Name} is unreachable.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return HealthCheckResult.Unhealthy(
                    $"Database readiness probe for {typeof(TDbContext).Name} failed.",
                    exception);
            }
        });
    }
}

namespace Gma.Framework.Api.Production;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public static class GmaHealthCheckTags
{
    public const string Readiness = "ready";
}

public static class GmaHealthCheckExtensions
{
    public static IServiceCollection AddGmaReadinessCheck(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> check)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(check);

        services.AddHealthChecks().Add(new HealthCheckRegistration(
            name.Trim(),
            provider => new DelegateReadinessHealthCheck(provider, check),
            HealthStatus.Unhealthy,
            [GmaHealthCheckTags.Readiness]));

        return services;
    }

    public static IEndpointRouteBuilder MapGmaHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(GmaHealthCheckTags.Readiness)
        });

        return endpoints;
    }

    private sealed class DelegateReadinessHealthCheck(
        IServiceProvider serviceProvider,
        Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> check) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            check(serviceProvider, cancellationToken);
    }
}

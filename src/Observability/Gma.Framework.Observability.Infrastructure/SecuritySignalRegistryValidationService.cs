namespace Gma.Framework.Observability.Infrastructure;

using Microsoft.Extensions.Hosting;

internal sealed class SecuritySignalRegistryValidationService(
    SecuritySignalRegistry registry) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = registry;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

namespace Gma.Framework.Caching.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Caching;

internal sealed class CachingStartupValidator(
    IServiceProvider serviceProvider,
    IOptions<CachingOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = CachingCompositionGuard.EnsureValid(options.Value, serviceProvider);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

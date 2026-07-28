namespace Gma.Framework.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class SecuritySignalServiceCollectionExtensions
{
    public static IServiceCollection AddSecuritySignalCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISecuritySignalRecorder, NullSecuritySignalRecorder>();
        return services;
    }

    private sealed class NullSecuritySignalRecorder : ISecuritySignalRecorder
    {
        public SecuritySignalReceipt Record(
            SecuritySignalDefinition definition,
            Guid? correlationId = null)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return new(SecuritySignalCorrelation.Create(correlationId), WasEmitted: false);
        }
    }
}

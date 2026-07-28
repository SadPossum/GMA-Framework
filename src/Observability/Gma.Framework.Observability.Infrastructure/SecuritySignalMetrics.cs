namespace Gma.Framework.Observability.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Gma.Framework.Observability;
using Gma.Framework.Runtime;
using Microsoft.Extensions.Options;

internal sealed class SecuritySignalMetrics
{
    private readonly Counter<long> occurrences;

    public SecuritySignalMetrics(
        IMeterFactory meterFactory,
        IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(
            ObservabilityMeterNames.ApplicationFor(applicationNamespace));
        this.occurrences = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.SecuritySignalsFor(applicationNamespace),
            description: "Number of payload-free security signal occurrences.");
    }

    public void Record(SecuritySignalDefinition definition)
    {
        TagList tags = new()
        {
            { ObservabilityTagNames.SecuritySignal, definition.Code },
            {
                ObservabilityTagNames.SecurityCategory,
                SecuritySignalCategories.ToWireName(definition.Category)
            },
            {
                ObservabilityTagNames.SecuritySeverity,
                SecuritySignalSeverities.ToWireName(definition.Severity)
            },
        };
        this.occurrences.Add(1, tags);
    }
}

namespace Gma.Framework.Messaging.Infrastructure;

using System.Diagnostics.Metrics;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;
using Microsoft.Extensions.Options;

public sealed class MessageJournalMetrics
{
    private readonly Counter<long> deleted;

    public MessageJournalMetrics(
        IMeterFactory meterFactory,
        IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(ObservabilityMeterNames.MessagingFor(applicationNamespace));
        this.deleted = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.MessageJournalDeletedFor(applicationNamespace),
            unit: "{message}",
            description: "Number of terminal message journal rows deleted by retention cleanup.");
    }

    public void RecordDeleted(string moduleName, string journal, int count)
    {
        if (count <= 0)
        {
            return;
        }

        this.deleted.Add(
            count,
            new KeyValuePair<string, object?>(
                ObservabilityTagNames.Module,
                IntegrationEventNaming.NormalizeModuleName(moduleName)),
            new KeyValuePair<string, object?>(ObservabilityTagNames.Operation, journal));
    }
}

namespace Gma.Framework.Messaging.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;

public sealed class InboxMetrics
{
    private readonly Counter<long> messages;
    private readonly Histogram<double> processDuration;

    public InboxMetrics(IMeterFactory meterFactory, IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(ObservabilityMeterNames.MessagingFor(applicationNamespace));
        this.messages = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.InboxMessagesFor(applicationNamespace),
            description: "Number of inbox message processing outcomes.");
        this.processDuration = meter.CreateHistogram<double>(
            ObservabilityInstrumentNames.InboxProcessDurationFor(applicationNamespace),
            unit: "ms",
            description: "Inbox message processing duration in milliseconds.");
    }

    public void RecordProcessed(
        string moduleName,
        string handlerName,
        string subject,
        InboxProcessStatus status,
        TimeSpan elapsed)
    {
        TagList tags = new()
        {
            { ObservabilityTagNames.Module, MetricTagValues.Module(moduleName) },
            { ObservabilityTagNames.Operation, MetricTagValues.Operation(handlerName) },
            { ObservabilityTagNames.Subject, IntegrationEventNaming.NormalizeSubject(subject) },
            { ObservabilityTagNames.Result, MetricTagValues.Result(NormalizeStatus(status)) }
        };

        this.messages.Add(1, tags);
        this.processDuration.Record(elapsed.TotalMilliseconds, tags);
    }

    private static string NormalizeStatus(InboxProcessStatus status) =>
        status switch
        {
            InboxProcessStatus.Processed => "processed",
            InboxProcessStatus.Duplicate => "duplicate",
            InboxProcessStatus.Failed => "failed",
            _ => "unknown"
        };
}

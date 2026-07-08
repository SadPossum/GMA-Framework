namespace Gma.Framework.Notifications.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Gma.Framework.Notifications;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;

public sealed class NotificationMetrics
{
    private readonly Counter<long> published;
    private readonly Counter<long> delivered;
    private readonly Histogram<double> deliveryDuration;

    public NotificationMetrics(IMeterFactory meterFactory, IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(ObservabilityMeterNames.NotificationsFor(applicationNamespace));
        this.published = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.NotificationsPublishedFor(applicationNamespace),
            description: "Number of user notifications accepted or bypassed by the notification publisher.");
        this.delivered = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.NotificationsDeliveredFor(applicationNamespace),
            description: "Number of user notification sink delivery attempts.");
        this.deliveryDuration = meter.CreateHistogram<double>(
            ObservabilityInstrumentNames.NotificationsDeliveryDurationFor(applicationNamespace),
            unit: "ms",
            description: "User notification sink delivery duration in milliseconds.");
    }

    public void RecordPublished(string moduleName, string notificationName, string result)
    {
        TagList tags = BaseTags(moduleName, notificationName, provider: "runtime", result);
        this.published.Add(1, tags);
    }

    public void RecordDelivery(
        string moduleName,
        string notificationName,
        string provider,
        string result,
        TimeSpan elapsed)
    {
        TagList tags = BaseTags(moduleName, notificationName, provider, result);
        this.delivered.Add(1, tags);
        this.deliveryDuration.Record(elapsed.TotalMilliseconds, tags);
    }

    private static TagList BaseTags(string moduleName, string notificationName, string provider, string result) =>
        new()
        {
            { ObservabilityTagNames.Module, MetricTagValues.Module(moduleName) },
            { ObservabilityTagNames.Operation, NotificationNames.NormalizeName(notificationName) },
            { ObservabilityTagNames.Provider, MetricTagValues.Provider(provider) },
            { ObservabilityTagNames.Result, MetricTagValues.Result(result) }
        };
}

namespace Gma.Framework.Messaging.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;

public sealed class OutboxMetrics
{
    private readonly Counter<long> claimed;
    private readonly Counter<long> published;
    private readonly Counter<long> failed;
    private readonly Histogram<double> publishDuration;
    private readonly ConcurrentDictionary<string, OutboxBacklogSnapshot> backlogByModule =
        new(StringComparer.Ordinal);

    public OutboxMetrics(IMeterFactory meterFactory, IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(ObservabilityMeterNames.MessagingFor(applicationNamespace));
        this.claimed = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.OutboxClaimedFor(applicationNamespace),
            description: "Number of outbox messages claimed for publishing.");
        this.published = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.OutboxPublishedFor(applicationNamespace),
            description: "Number of outbox messages published successfully.");
        this.failed = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.OutboxFailedFor(applicationNamespace),
            description: "Number of outbox publish attempts that failed.");
        this.publishDuration = meter.CreateHistogram<double>(
            ObservabilityInstrumentNames.OutboxPublishDurationFor(applicationNamespace),
            unit: "ms",
            description: "Outbox publish attempt duration in milliseconds.");
        meter.CreateObservableGauge(
            ObservabilityInstrumentNames.OutboxBacklogFor(applicationNamespace),
            this.ObserveBacklog,
            unit: "{message}",
            description: "Number of pending outbox messages by module.");
        meter.CreateObservableGauge(
            ObservabilityInstrumentNames.OutboxExhaustedFor(applicationNamespace),
            this.ObserveExhausted,
            unit: "{message}",
            description: "Number of outbox messages that exhausted automatic attempts by module.");
        meter.CreateObservableGauge(
            ObservabilityInstrumentNames.OutboxOldestPendingAgeFor(applicationNamespace),
            this.ObserveOldestPendingAge,
            unit: "s",
            description: "Age in seconds of the oldest pending outbox message by module.");
    }

    public void RecordClaimed(string moduleName, int count)
    {
        if (count <= 0)
        {
            return;
        }

        this.claimed.Add(
            count,
            new KeyValuePair<string, object?>(ObservabilityTagNames.Module, MetricTagValues.Module(moduleName)));
    }

    public void RecordPublished(string moduleName, string subject, TimeSpan elapsed) =>
        this.RecordPublishAttempt(moduleName, subject, isSuccess: true, elapsed);

    public void RecordFailed(string moduleName, string subject, TimeSpan elapsed) =>
        this.RecordPublishAttempt(moduleName, subject, isSuccess: false, elapsed);

    public void RecordBacklog(OutboxBacklogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string moduleName = IntegrationEventNaming.NormalizeModuleName(snapshot.ModuleName);
        this.backlogByModule[moduleName] = snapshot;
    }

    private IEnumerable<Measurement<long>> ObserveBacklog() =>
        this.backlogByModule.Select(pair => new Measurement<long>(
            pair.Value.PendingCount,
            new KeyValuePair<string, object?>(ObservabilityTagNames.Module, pair.Key)));

    private IEnumerable<Measurement<long>> ObserveExhausted() =>
        this.backlogByModule.Select(pair => new Measurement<long>(
            pair.Value.ExhaustedCount,
            new KeyValuePair<string, object?>(ObservabilityTagNames.Module, pair.Key)));

    private IEnumerable<Measurement<double>> ObserveOldestPendingAge() =>
        this.backlogByModule.Select(pair => new Measurement<double>(
            Math.Max(0, pair.Value.OldestPendingAge.TotalSeconds),
            new KeyValuePair<string, object?>(ObservabilityTagNames.Module, pair.Key)));

    private void RecordPublishAttempt(string moduleName, string subject, bool isSuccess, TimeSpan elapsed)
    {
        TagList tags = new()
        {
            { ObservabilityTagNames.Module, MetricTagValues.Module(moduleName) },
            { ObservabilityTagNames.Subject, IntegrationEventNaming.NormalizeSubject(subject) },
            { ObservabilityTagNames.Result, MetricTagValues.Result(isSuccess ? "success" : "failure") },
        };

        if (isSuccess)
        {
            this.published.Add(1, tags);
        }
        else
        {
            this.failed.Add(1, tags);
        }

        this.publishDuration.Record(elapsed.TotalMilliseconds, tags);
    }
}

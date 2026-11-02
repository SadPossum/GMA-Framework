namespace Gma.Framework.Messaging.Infrastructure;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Gma.Framework.Messaging;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;

public sealed class MessageJournalMetrics
{
    private readonly Counter<long> deleted;
    private readonly Counter<long> failures;
    private readonly Histogram<double> cleanupDuration;
    private readonly ConcurrentDictionary<JournalIdentity, DateTimeOffset> oldestProcessed = new();
    private readonly ISystemClock clock;

    public MessageJournalMetrics(
        IMeterFactory meterFactory,
        IOptions<ApplicationIdentityOptions> applicationIdentity,
        ISystemClock clock)
    {
        this.clock = clock;
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(ObservabilityMeterNames.MessagingFor(applicationNamespace));
        this.deleted = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.MessageJournalDeletedFor(applicationNamespace),
            unit: "{message}",
            description: "Number of terminal message journal rows deleted by retention cleanup.");
        this.failures = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.MessageJournalCleanupFailuresFor(applicationNamespace),
            unit: "{failure}",
            description: "Number of failed message journal cleanup attempts.");
        this.cleanupDuration = meter.CreateHistogram<double>(
            ObservabilityInstrumentNames.MessageJournalCleanupDurationFor(applicationNamespace),
            unit: "ms",
            description: "Duration of one module journal cleanup attempt.");
        meter.CreateObservableGauge(
            ObservabilityInstrumentNames.MessageJournalOldestProcessedAgeFor(applicationNamespace),
            this.ObserveOldestProcessedAge,
            unit: "s",
            description: "Age of the oldest retained processed message journal row.");
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

    public void RecordFailure(string moduleName, string journal) =>
        this.failures.Add(1, CreateTags(moduleName, journal));

    public void RecordDuration(string moduleName, string journal, TimeSpan duration) =>
        this.cleanupDuration.Record(Math.Max(0, duration.TotalMilliseconds), CreateTags(moduleName, journal));

    public void SetOldestProcessed(
        string moduleName,
        string journal,
        DateTimeOffset? processedAtUtc)
    {
        JournalIdentity identity = CreateIdentity(moduleName, journal);
        if (processedAtUtc is null)
        {
            this.oldestProcessed.TryRemove(identity, out _);
            return;
        }

        this.oldestProcessed[identity] = processedAtUtc.Value;
    }

    private IEnumerable<Measurement<double>> ObserveOldestProcessedAge()
    {
        DateTimeOffset nowUtc = this.clock.UtcNow;
        foreach (KeyValuePair<JournalIdentity, DateTimeOffset> pair in this.oldestProcessed)
        {
            yield return new Measurement<double>(
                Math.Max(0, (nowUtc - pair.Value).TotalSeconds),
                CreateTags(pair.Key.ModuleName, pair.Key.Journal));
        }
    }

    private static KeyValuePair<string, object?>[] CreateTags(string moduleName, string journal)
    {
        JournalIdentity identity = CreateIdentity(moduleName, journal);
        return
        [
            new(ObservabilityTagNames.Module, identity.ModuleName),
            new(ObservabilityTagNames.Operation, identity.Journal),
        ];
    }

    private static JournalIdentity CreateIdentity(string moduleName, string journal) =>
        new(
            IntegrationEventNaming.NormalizeModuleName(moduleName),
            journal is "outbox" or "inbox"
                ? journal
                : throw new ArgumentException("Journal must be 'outbox' or 'inbox'.", nameof(journal)));

    private readonly record struct JournalIdentity(string ModuleName, string Journal);
}

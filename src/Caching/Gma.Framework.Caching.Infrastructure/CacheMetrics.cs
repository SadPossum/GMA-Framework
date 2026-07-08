namespace Gma.Framework.Caching.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;

internal sealed class CacheMetrics(IMeterFactory meterFactory, IOptions<ApplicationIdentityOptions> applicationIdentity)
{
    private readonly Counter<long> requests = meterFactory
        .Create(ObservabilityMeterNames.CachingFor(applicationIdentity.Value.EffectiveNamespace))
        .CreateCounter<long>(ObservabilityInstrumentNames.CacheRequestsFor(applicationIdentity.Value.EffectiveNamespace));
    private readonly Histogram<double> duration = meterFactory
        .Create(ObservabilityMeterNames.CachingFor(applicationIdentity.Value.EffectiveNamespace))
        .CreateHistogram<double>(ObservabilityInstrumentNames.CacheDurationFor(applicationIdentity.Value.EffectiveNamespace), "ms");
    private readonly Counter<long> backendFailures = meterFactory
        .Create(ObservabilityMeterNames.CachingFor(applicationIdentity.Value.EffectiveNamespace))
        .CreateCounter<long>(ObservabilityInstrumentNames.CacheBackendFailuresFor(applicationIdentity.Value.EffectiveNamespace));
    private readonly Counter<long> invalidationFailures = meterFactory
        .Create(ObservabilityMeterNames.CachingFor(applicationIdentity.Value.EffectiveNamespace))
        .CreateCounter<long>(ObservabilityInstrumentNames.CacheInvalidationFailuresFor(applicationIdentity.Value.EffectiveNamespace));

    public void RecordRequest(string module, string provider, string result, TimeSpan elapsed)
    {
        TagList tags = CreateTags(module, "read", provider, result);
        this.requests.Add(1, tags);
        this.duration.Record(elapsed.TotalMilliseconds, tags);
    }

    public void RecordBackendFailure(string module, string operation, string provider) =>
        this.backendFailures.Add(1, CreateTags(module, operation, provider, "failure"));

    public void RecordInvalidationFailure(string module, string operation, string provider) =>
        this.invalidationFailures.Add(1, CreateTags(module, operation, provider, "failure"));

    private static TagList CreateTags(string module, string operation, string provider, string result) => new()
    {
        { ObservabilityTagNames.Module, MetricTagValues.Module(module) },
        { ObservabilityTagNames.Operation, MetricTagValues.Operation(operation) },
        { ObservabilityTagNames.Provider, MetricTagValues.Provider(provider) },
        { ObservabilityTagNames.Result, MetricTagValues.Result(result) }
    };
}

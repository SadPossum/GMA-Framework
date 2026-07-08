namespace Gma.Framework.Observability.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Gma.Framework.Observability;
using Gma.Framework.Runtime;

public sealed class CommandMetrics
{
    private readonly Counter<long> executed;
    private readonly Histogram<double> duration;

    public CommandMetrics(IMeterFactory meterFactory, IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(ObservabilityMeterNames.ApplicationFor(applicationNamespace));
        this.executed = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.CommandsExecutedFor(applicationNamespace),
            description: "Number of commands completed by the application pipeline.");
        this.duration = meter.CreateHistogram<double>(
            ObservabilityInstrumentNames.CommandsDurationFor(applicationNamespace),
            unit: "ms",
            description: "Command execution duration in milliseconds.");
    }

    public void Record(string moduleName, string commandName, bool isSuccess, string? errorCode, TimeSpan elapsed)
    {
        string? normalizedErrorCode = MetricTagValues.ErrorCode(errorCode);
        TagList tags = new()
        {
            { ObservabilityTagNames.Module, MetricTagValues.Module(moduleName) },
            { ObservabilityTagNames.Operation, MetricTagValues.Operation(commandName) },
            { ObservabilityTagNames.Result, MetricTagValues.Result(isSuccess ? "success" : "failure") },
        };

        if (normalizedErrorCode is not null)
        {
            tags.Add(ObservabilityTagNames.ErrorCode, normalizedErrorCode);
        }

        this.executed.Add(1, tags);
        this.duration.Record(elapsed.TotalMilliseconds, tags);
    }
}

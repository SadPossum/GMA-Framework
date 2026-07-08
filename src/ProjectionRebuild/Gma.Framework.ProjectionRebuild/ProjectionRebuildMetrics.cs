namespace Gma.Framework.ProjectionRebuild;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;

public sealed class ProjectionRebuildMetrics
{
    private readonly Counter<long> batches;
    private readonly Counter<long> rows;
    private readonly Counter<long> failures;
    private readonly Histogram<double> batchDuration;

    public ProjectionRebuildMetrics(
        IMeterFactory meterFactory,
        IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentNullException.ThrowIfNull(applicationIdentity);

        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create($"{applicationNamespace}.projections");
        this.batches = meter.CreateCounter<long>(
            $"{applicationNamespace}.projections.rebuild.batches",
            description: "Projection rebuild batches processed.");
        this.rows = meter.CreateCounter<long>(
            $"{applicationNamespace}.projections.rebuild.rows",
            description: "Projection rebuild rows processed.");
        this.failures = meter.CreateCounter<long>(
            $"{applicationNamespace}.projections.rebuild.failures",
            description: "Projection rebuild failures.");
        this.batchDuration = meter.CreateHistogram<double>(
            $"{applicationNamespace}.projections.rebuild.batch.duration",
            unit: "ms",
            description: "Projection rebuild batch duration in milliseconds.");
    }

    public void RecordBatch(
        string moduleName,
        string projectionName,
        bool dryRun,
        long processedCount,
        TimeSpan elapsed)
    {
        TagList tags = CreateTags(moduleName, projectionName, dryRun, "success");
        this.batches.Add(1, tags);
        this.rows.Add(processedCount, tags);
        this.batchDuration.Record(elapsed.TotalMilliseconds, tags);
    }

    public void RecordFailure(
        string moduleName,
        string projectionName,
        bool dryRun)
    {
        TagList tags = CreateTags(moduleName, projectionName, dryRun, "failure");
        this.failures.Add(1, tags);
    }

    private static TagList CreateTags(
        string moduleName,
        string projectionName,
        bool dryRun,
        string result) =>
        new()
        {
            { ObservabilityTagNames.Module, MetricTagValues.Module(moduleName) },
            { ObservabilityTagNames.Operation, MetricTagValues.Operation(projectionName) },
            { "mode", dryRun ? "dry-run" : "write" },
            { ObservabilityTagNames.Result, MetricTagValues.Result(result) },
        };
}

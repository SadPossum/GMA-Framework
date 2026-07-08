namespace Gma.Framework.Tasks.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Tasks;

public sealed class TaskMetrics
{
    private readonly Counter<long> claimed;
    private readonly Counter<long> completed;
    private readonly Counter<long> timedOut;
    private readonly Histogram<double> duration;
    private readonly TaskMetricsSnapshotStore snapshots;

    public TaskMetrics(
        IMeterFactory meterFactory,
        TaskMetricsSnapshotStore snapshots,
        IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentNullException.ThrowIfNull(snapshots);

        this.snapshots = snapshots;
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create(ObservabilityMeterNames.TasksFor(applicationNamespace));
        this.claimed = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.TaskClaimedFor(applicationNamespace),
            description: "Number of task runs claimed by workers.");
        this.completed = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.TaskCompletedFor(applicationNamespace),
            description: "Number of task runs completed by workers.");
        this.timedOut = meter.CreateCounter<long>(
            ObservabilityInstrumentNames.TaskTimedOutFor(applicationNamespace),
            description: "Number of stale task runs marked timed out.");
        this.duration = meter.CreateHistogram<double>(
            ObservabilityInstrumentNames.TaskDurationFor(applicationNamespace),
            unit: "ms",
            description: "Task handler execution duration in milliseconds.");
        _ = meter.CreateObservableGauge(
            ObservabilityInstrumentNames.TaskQueueDepthFor(applicationNamespace),
            snapshots.ObserveQueueDepth,
            unit: "{runs}",
            description: "Current queued or retry-scheduled task runs by status.");
        _ = meter.CreateObservableGauge(
            ObservabilityInstrumentNames.TaskActiveRunsFor(applicationNamespace),
            snapshots.ObserveActiveRuns,
            unit: "{runs}",
            description: "Current leased, running, or cancellation-requested task runs by status.");
    }

    public void RecordClaimed(string moduleName, string taskName, string workerGroup)
    {
        TagList tags = CreateTags(moduleName, taskName, workerGroup, "claimed");
        this.claimed.Add(1, tags);
    }

    public void RecordCompleted(
        string moduleName,
        string taskName,
        string workerGroup,
        string result,
        TimeSpan elapsed)
    {
        TagList tags = CreateTags(moduleName, taskName, workerGroup, result);
        this.completed.Add(1, tags);
        this.duration.Record(elapsed.TotalMilliseconds, tags);
    }

    public void RecordTimedOut(string moduleName, string taskName, string workerGroup)
    {
        TagList tags = CreateTags(moduleName, taskName, workerGroup, "timed-out");
        this.timedOut.Add(1, tags);
    }

    public void RecordSnapshot(TaskRunStats stats) =>
        this.snapshots.Update(stats);

    private static TagList CreateTags(
        string moduleName,
        string taskName,
        string workerGroup,
        string result) =>
        new()
        {
            { ObservabilityTagNames.Module, MetricTagValues.Module(moduleName) },
            { ObservabilityTagNames.Operation, MetricTagValues.Operation(taskName) },
            { "worker.group", MetricTagValues.Operation(workerGroup) },
            { ObservabilityTagNames.Result, MetricTagValues.Result(result) },
        };
}

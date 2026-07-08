namespace Gma.Framework.Tasks.Infrastructure;

using System.Diagnostics.Metrics;
using System.Threading;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Tasks;

public sealed class TaskMetricsSnapshotStore
{
    private static readonly TaskRunStatus[] QueueStatuses =
    [
        TaskRunStatus.Queued,
        TaskRunStatus.RetryScheduled
    ];

    private static readonly TaskRunStatus[] ActiveStatuses =
    [
        TaskRunStatus.Leased,
        TaskRunStatus.Running,
        TaskRunStatus.CancellationRequested
    ];

    private readonly Lock sync = new();
    private TaskRunStatusCount[] statusCounts = [];

    public void Update(TaskRunStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        lock (this.sync)
        {
            this.statusCounts = stats.StatusCounts.ToArray();
        }
    }

    public IEnumerable<Measurement<long>> ObserveQueueDepth() =>
        this.Observe(QueueStatuses);

    public IEnumerable<Measurement<long>> ObserveActiveRuns() =>
        this.Observe(ActiveStatuses);

    private IEnumerable<Measurement<long>> Observe(IReadOnlyList<TaskRunStatus> statuses)
    {
        TaskRunStatusCount[] snapshot;
        lock (this.sync)
        {
            snapshot = this.statusCounts;
        }

        Dictionary<TaskRunStatus, int> counts = snapshot.ToDictionary(item => item.Status, item => item.Count);
        foreach (TaskRunStatus status in statuses)
        {
            counts.TryGetValue(status, out int count);
            yield return new Measurement<long>(
                count,
                new KeyValuePair<string, object?>(ObservabilityTagNames.TaskStatus, TaskRunStatusNames.ToWireName(status)));
        }
    }
}

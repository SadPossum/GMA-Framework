namespace Gma.Framework.Tasks;

public sealed record TaskWorkerClaim
{
    public TaskWorkerClaim(
        string workerGroup,
        string workerId,
        string nodeId,
        DateTimeOffset claimedAtUtc,
        int maxRuns,
        TimeSpan leaseDuration)
    {
        this.WorkerGroup = TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup));
        this.WorkerId = TaskNames.NormalizeWorkerId(workerId, nameof(workerId));
        this.NodeId = TaskNames.NormalizeWorkerId(nodeId, nameof(nodeId));
        this.ClaimedAtUtc = TaskRunRequest.RequireTimestamp(claimedAtUtc, nameof(claimedAtUtc));
        this.MaxRuns = maxRuns > 0
            ? maxRuns
            : throw new ArgumentOutOfRangeException(nameof(maxRuns), maxRuns, "Task claim batch size must be positive.");
        this.LeaseDuration = leaseDuration > TimeSpan.Zero
            ? leaseDuration
            : throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Task lease duration must be positive.");
    }

    public string WorkerGroup { get; }
    public string WorkerId { get; }
    public string NodeId { get; }
    public DateTimeOffset ClaimedAtUtc { get; }
    public int MaxRuns { get; }
    public TimeSpan LeaseDuration { get; }
    public DateTimeOffset LockedUntilUtc => this.ClaimedAtUtc.Add(this.LeaseDuration);
}

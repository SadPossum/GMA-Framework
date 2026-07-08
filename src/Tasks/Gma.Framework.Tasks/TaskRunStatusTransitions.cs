namespace Gma.Framework.Tasks;

public static class TaskRunStatusTransitions
{
    public static bool IsTerminal(TaskRunStatus status) =>
        RequireKnown(status) is TaskRunStatus.Succeeded or
            TaskRunStatus.Failed or
            TaskRunStatus.Canceled or
            TaskRunStatus.TimedOut;

    public static bool CanClaim(
        TaskRunStatus status,
        DateTimeOffset scheduledAtUtc,
        DateTimeOffset? lockedUntilUtc,
        DateTimeOffset? nextAttemptAtUtc,
        DateTimeOffset nowUtc)
    {
        status = RequireKnown(status);
        _ = TaskRunRequest.RequireTimestamp(scheduledAtUtc, nameof(scheduledAtUtc));
        _ = TaskRunRequest.RequireTimestamp(nowUtc, nameof(nowUtc));

        if (IsTerminal(status) ||
            status is TaskRunStatus.WaitingForControl ||
            scheduledAtUtc > nowUtc ||
            nextAttemptAtUtc > nowUtc)
        {
            return false;
        }

        if (lockedUntilUtc is not null && lockedUntilUtc > nowUtc)
        {
            return false;
        }

        return status is TaskRunStatus.Queued or
            TaskRunStatus.Leased or
            TaskRunStatus.Running or
            TaskRunStatus.CancellationRequested or
            TaskRunStatus.RetryScheduled;
    }

    public static bool CanStart(TaskRunStatus status) =>
        RequireKnown(status) == TaskRunStatus.Leased;

    public static bool CanComplete(TaskRunStatus status) =>
        RequireKnown(status) is TaskRunStatus.Running or TaskRunStatus.CancellationRequested;

    public static bool CanRequestCancellation(TaskRunStatus status)
    {
        status = RequireKnown(status);
        return !IsTerminal(status) && status != TaskRunStatus.CancellationRequested;
    }

    public static TaskRunStatus RequireKnown(TaskRunStatus status) =>
        status == TaskRunStatus.Unknown || !Enum.IsDefined(status)
            ? throw new ArgumentOutOfRangeException(nameof(status), status, "Task run status must be known.")
            : status;
}

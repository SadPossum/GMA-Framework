namespace Gma.Framework.Tasks;

public static class TaskRunStatusNames
{
    public const string Queued = "queued";
    public const string Leased = "leased";
    public const string Running = "running";
    public const string WaitingForControl = "waiting-for-control";
    public const string RetryScheduled = "retry-scheduled";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string CancellationRequested = "cancellation-requested";
    public const string Canceled = "canceled";
    public const string TimedOut = "timed-out";

    public static string ToWireName(TaskRunStatus status) =>
        TaskRunStatusTransitions.RequireKnown(status) switch
        {
            TaskRunStatus.Queued => Queued,
            TaskRunStatus.Leased => Leased,
            TaskRunStatus.Running => Running,
            TaskRunStatus.WaitingForControl => WaitingForControl,
            TaskRunStatus.RetryScheduled => RetryScheduled,
            TaskRunStatus.Succeeded => Succeeded,
            TaskRunStatus.Failed => Failed,
            TaskRunStatus.CancellationRequested => CancellationRequested,
            TaskRunStatus.Canceled => Canceled,
            TaskRunStatus.TimedOut => TimedOut,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Task run status must be known.")
        };

    public static bool TryParse(string? value, out TaskRunStatus status)
    {
        status = TaskRunStatus.Unknown;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        status = normalized switch
        {
            Queued => TaskRunStatus.Queued,
            Leased => TaskRunStatus.Leased,
            Running => TaskRunStatus.Running,
            WaitingForControl or "waitingforcontrol" => TaskRunStatus.WaitingForControl,
            RetryScheduled or "retryscheduled" => TaskRunStatus.RetryScheduled,
            Succeeded => TaskRunStatus.Succeeded,
            Failed => TaskRunStatus.Failed,
            CancellationRequested or "cancellationrequested" => TaskRunStatus.CancellationRequested,
            Canceled or "cancelled" => TaskRunStatus.Canceled,
            TimedOut or "timedout" => TaskRunStatus.TimedOut,
            _ => TaskRunStatus.Unknown
        };

        return status != TaskRunStatus.Unknown;
    }

    public static bool TryParseOptional(string? value, out TaskRunStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParse(value, out TaskRunStatus parsedStatus))
        {
            return false;
        }

        status = parsedStatus;
        return true;
    }
}

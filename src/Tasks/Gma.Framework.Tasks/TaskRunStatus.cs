namespace Gma.Framework.Tasks;

public enum TaskRunStatus
{
    Unknown = 0,
    Queued = 1,
    Leased = 2,
    Running = 3,
    WaitingForControl = 4,
    RetryScheduled = 5,
    Succeeded = 6,
    Failed = 7,
    CancellationRequested = 8,
    Canceled = 9,
    TimedOut = 10
}

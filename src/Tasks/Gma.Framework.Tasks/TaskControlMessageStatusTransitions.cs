namespace Gma.Framework.Tasks;

public static class TaskControlMessageStatusTransitions
{
    public static bool IsReadable(TaskControlMessageStatus status) =>
        RequireKnown(status) is TaskControlMessageStatus.Pending or
            TaskControlMessageStatus.Delivered or
            TaskControlMessageStatus.Failed;

    public static bool CanMarkDelivered(TaskControlMessageStatus status) =>
        IsReadable(status);

    public static bool CanMarkHandled(TaskControlMessageStatus status) =>
        RequireKnown(status) is TaskControlMessageStatus.Pending or
            TaskControlMessageStatus.Delivered or
            TaskControlMessageStatus.Failed;

    public static bool CanMarkFailed(TaskControlMessageStatus status) =>
        RequireKnown(status) is TaskControlMessageStatus.Pending or
            TaskControlMessageStatus.Delivered or
            TaskControlMessageStatus.Failed;

    public static TaskControlMessageStatus RequireKnown(TaskControlMessageStatus status) =>
        status == TaskControlMessageStatus.Unknown || !Enum.IsDefined(status)
            ? throw new ArgumentOutOfRangeException(nameof(status), status, "Task control message status must be known.")
            : status;
}

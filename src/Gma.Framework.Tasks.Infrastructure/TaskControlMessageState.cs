namespace Gma.Framework.Tasks.Infrastructure;

using Gma.Framework.Tasks;

public class TaskControlMessageState
{
    public const int CommandNameMaxLength = TaskNames.ControlCommandMaxLength;
    public const int RequestedByMaxLength = TaskNames.ActorMaxLength;

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public string CommandName { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset EnqueuedAtUtc { get; private set; }
    public string? RequestedBy { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public TaskControlMessageStatus Status { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private TaskControlMessageState() { }

    private TaskControlMessageState(TaskControlMessage message)
    {
        this.Id = message.MessageId;
        this.RunId = message.RunId;
        this.CommandName = message.CommandName;
        this.Payload = message.PayloadJson;
        this.EnqueuedAtUtc = message.EnqueuedAtUtc;
        this.RequestedBy = message.RequestedBy;
        this.ExpiresAtUtc = message.ExpiresAtUtc;
        this.Status = TaskControlMessageStatus.Pending;
    }

    public static TaskControlMessageState Enqueue(TaskControlMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new TaskControlMessageState(message);
    }

    public void MarkDelivered(DateTimeOffset nowUtc)
    {
        if (!TaskControlMessageStatusTransitions.CanMarkDelivered(this.Status))
        {
            return;
        }

        this.Status = TaskControlMessageStatus.Delivered;
        this.DeliveredAtUtc = TaskRun.RequireTimestamp(nowUtc, nameof(nowUtc));
    }

    public void MarkHandled(DateTimeOffset nowUtc)
    {
        if (!TaskControlMessageStatusTransitions.CanMarkHandled(this.Status))
        {
            return;
        }

        this.Status = TaskControlMessageStatus.Handled;
        this.CompletedAtUtc = TaskRun.RequireTimestamp(nowUtc, nameof(nowUtc));
        this.LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset nowUtc)
    {
        if (!TaskControlMessageStatusTransitions.CanMarkFailed(this.Status))
        {
            return;
        }

        this.Status = TaskControlMessageStatus.Failed;
        this.CompletedAtUtc = TaskRun.RequireTimestamp(nowUtc, nameof(nowUtc));
        this.LastError = TaskRun.NormalizeError(error);
    }

    public bool IsReadableAt(DateTimeOffset nowUtc)
    {
        if (!TaskControlMessageStatusTransitions.IsReadable(this.Status))
        {
            return false;
        }

        return this.ExpiresAtUtc is null || this.ExpiresAtUtc > nowUtc;
    }
}

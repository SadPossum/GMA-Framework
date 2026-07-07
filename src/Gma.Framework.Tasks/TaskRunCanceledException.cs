namespace Gma.Framework.Tasks;

public sealed class TaskRunCanceledException(string? message = null)
    : OperationCanceledException(string.IsNullOrWhiteSpace(message) ? "Task run was canceled by a control signal." : message)
{
}

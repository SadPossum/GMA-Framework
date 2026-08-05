namespace Gma.Framework.Tasks;

public sealed class TaskScopeNotAcceptingWorkException()
    : InvalidOperationException("The task scope is not accepting new work.");

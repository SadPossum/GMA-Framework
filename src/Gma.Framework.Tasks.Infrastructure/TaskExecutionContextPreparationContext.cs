namespace Gma.Framework.Tasks.Infrastructure;

using Gma.Framework.Tasks;

public sealed record TaskExecutionContextPreparationContext
{
    public TaskExecutionContextPreparationContext(
        TaskRunLease lease,
        TaskHandlerRegistration registration,
        TaskExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(executionContext);

        this.Lease = lease;
        this.Registration = registration;
        this.ExecutionContext = executionContext;
    }

    public TaskRunLease Lease { get; }
    public TaskHandlerRegistration Registration { get; }
    public TaskExecutionContext ExecutionContext { get; }
}

namespace Gma.Framework.Cqrs.UnitOfWork;

public interface IRollbackResettableUnitOfWork : ITransactionalUnitOfWork
{
    Task ResetAfterRollbackAsync(CancellationToken cancellationToken = default);
}

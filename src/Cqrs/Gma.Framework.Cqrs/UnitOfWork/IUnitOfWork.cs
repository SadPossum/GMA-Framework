namespace Gma.Framework.Cqrs.UnitOfWork;

public interface IUnitOfWork
{
    string ModuleName { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

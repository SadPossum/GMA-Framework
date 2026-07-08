namespace Gma.Framework.Cqrs.Infrastructure;

using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Results;
using Gma.Framework.Naming;
using Gma.Framework.Observability.Infrastructure;

internal sealed class CommandUnitOfWorkBehavior<TCommand, TResponse>(IEnumerable<IUnitOfWork> unitOfWorks)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        Result<TResponse> result = await next().ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result;
        }

        if (command is not ITransactionalCommand<TResponse>)
        {
            return result;
        }

        string moduleName = ModuleNameResolver.FromType(typeof(TCommand));
        IUnitOfWork[] moduleUnitOfWorks = unitOfWorks
            .Where(unitOfWork => string.Equals(
                NormalizeModuleName(unitOfWork.ModuleName),
                moduleName,
                StringComparison.Ordinal))
            .ToArray();

        IUnitOfWork unitOfWork = moduleUnitOfWorks.Length switch
        {
            1 => moduleUnitOfWorks[0],
            0 => throw new InvalidOperationException(
                $"Transactional command '{typeof(TCommand).FullName}' belongs to module '{moduleName}', but no matching unit of work is registered."),
            _ => throw new InvalidOperationException(
                $"Transactional command '{typeof(TCommand).FullName}' belongs to module '{moduleName}', but {moduleUnitOfWorks.Length} matching units of work are registered.")
        };

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static string NormalizeModuleName(string moduleName) =>
        SharedNameSegments.NormalizeKebabSegment(moduleName, "module name", nameof(moduleName));
}

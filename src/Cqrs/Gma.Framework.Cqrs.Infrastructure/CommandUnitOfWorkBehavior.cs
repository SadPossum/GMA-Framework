namespace Gma.Framework.Cqrs.Infrastructure;

using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Results;
using Gma.Framework.Naming;
using Gma.Framework.Observability.Infrastructure;
using System.Runtime.ExceptionServices;

internal sealed class CommandUnitOfWorkBehavior<TCommand, TResponse>(IEnumerable<IUnitOfWork> unitOfWorks)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    internal const string CleanupFailureDataKey = "Gma.Framework.Cqrs.UnitOfWorkCleanupFailure";

    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command is not ITransactionalCommand<TResponse>)
        {
            Result<TResponse> result = await next().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
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

        ITransactionalUnitOfWork? transactionalUnitOfWork = unitOfWork as ITransactionalUnitOfWork;
        bool cleanupAttempted = false;
        try
        {
            if (transactionalUnitOfWork is not null)
            {
                await transactionalUnitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Result<TResponse> result = await next().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.IsSuccess)
            {
                if (transactionalUnitOfWork is not null)
                {
                    cleanupAttempted = true;
                    await RollbackAndResetAsync(transactionalUnitOfWork).ConfigureAwait(false);
                }

                return result;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transactionalUnitOfWork is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await transactionalUnitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception operationException)
        {
            if (transactionalUnitOfWork is not null && !cleanupAttempted)
            {
                try
                {
                    await RollbackAndResetAsync(transactionalUnitOfWork).ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    TryRecordCleanupFailure(operationException, cleanupException);
                }
            }

            throw;
        }
    }

    private static async Task RollbackAndResetAsync(ITransactionalUnitOfWork unitOfWork)
    {
        Exception? rollbackException = null;
        try
        {
            await unitOfWork
                .RollbackTransactionAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            rollbackException = exception;
        }

        Exception? resetException = null;
        if (unitOfWork is IRollbackResettableUnitOfWork resettableUnitOfWork)
        {
            try
            {
                await resettableUnitOfWork
                    .ResetAfterRollbackAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                resetException = exception;
            }
        }

        if (rollbackException is not null && resetException is not null)
        {
            throw new AggregateException(
                "The transactional unit of work could not be rolled back or reset.",
                rollbackException,
                resetException);
        }

        Exception? cleanupException = rollbackException ?? resetException;
        if (cleanupException is not null)
        {
            ExceptionDispatchInfo.Capture(cleanupException).Throw();
        }
    }

    private static void TryRecordCleanupFailure(Exception operationException, Exception cleanupException)
    {
        try
        {
            operationException.Data[CleanupFailureDataKey] = cleanupException;
        }
        catch (Exception)
        {
            // Cleanup diagnostics must never replace the original operation failure.
        }
    }

    private static string NormalizeModuleName(string moduleName) =>
        SharedNameSegments.NormalizeKebabSegment(moduleName, "module name", nameof(moduleName));
}

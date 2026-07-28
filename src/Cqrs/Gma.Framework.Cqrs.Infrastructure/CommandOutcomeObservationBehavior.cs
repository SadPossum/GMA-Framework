namespace Gma.Framework.Cqrs.Infrastructure;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed partial class CommandOutcomeObservationBehavior<TCommand, TResponse>(
    IEnumerable<ICommandOutcomeObserver<TCommand, TResponse>> observers,
    IOptions<CommandOutcomeObservationOptions> options,
    ILogger<CommandOutcomeObservationBehavior<TCommand, TResponse>> logger)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        Result<TResponse> result = await next().ConfigureAwait(false);
        using CancellationTokenSource timeout = new(options.Value.Timeout);
        foreach (ICommandOutcomeObserver<TCommand, TResponse> observer in observers)
        {
            try
            {
                await observer
                    .ObserveAsync(command, result, timeout.Token)
                    .WaitAsync(timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                TryLogObservationTimeout(
                    logger,
                    typeof(TCommand).FullName ?? typeof(TCommand).Name,
                    options.Value.Timeout);
                break;
            }
            catch (Exception)
            {
                TryLogObserverFailure(
                    logger,
                    observer.GetType().FullName ?? observer.GetType().Name,
                    typeof(TCommand).FullName ?? typeof(TCommand).Name);
            }
        }

        return result;
    }

    private static void TryLogObserverFailure(
        ILogger logger,
        string observerType,
        string commandType)
    {
        try
        {
            LogObserverFailure(logger, observerType, commandType);
        }
        catch (Exception)
        {
            // Observation is post-settlement; logging cannot change the command result.
        }
    }

    private static void TryLogObservationTimeout(
        ILogger logger,
        string commandType,
        TimeSpan timeout)
    {
        try
        {
            LogObservationTimeout(logger, commandType, timeout);
        }
        catch (Exception)
        {
            // Observation is post-settlement; logging cannot change the command result.
        }
    }

    [LoggerMessage(
        EventId = 2302,
        Level = LogLevel.Warning,
        Message = "Command outcome observer {ObserverType} failed for command type {CommandType}.")]
    private static partial void LogObserverFailure(
        ILogger logger,
        string observerType,
        string commandType);

    [LoggerMessage(
        EventId = 2303,
        Level = LogLevel.Warning,
        Message = "Command outcome observation timed out for command type {CommandType} after {Timeout}.")]
    private static partial void LogObservationTimeout(
        ILogger logger,
        string commandType,
        TimeSpan timeout);
}

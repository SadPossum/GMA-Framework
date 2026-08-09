namespace Gma.Framework.Cqrs;

public sealed class TransactionCoordinationException(
    TransactionCoordinationFailure failure,
    Exception? innerException = null)
    : Exception(GetMessage(failure), innerException)
{
    public TransactionCoordinationFailure Failure { get; } = failure;

    private static string GetMessage(TransactionCoordinationFailure failure) => failure switch
    {
        TransactionCoordinationFailure.TimedOut =>
            "Transaction coordination timed out. Retry the complete operation.",
        TransactionCoordinationFailure.CanceledByProvider =>
            "Transaction coordination was canceled by the provider. Retry the complete operation.",
        TransactionCoordinationFailure.DeadlockVictim =>
            "Transaction coordination was selected as a deadlock victim. Retry the complete operation.",
        _ => throw new ArgumentOutOfRangeException(
            nameof(failure),
            failure,
            "Transaction coordination failure is invalid.")
    };
}

public enum TransactionCoordinationFailure
{
    TimedOut = 1,
    CanceledByProvider = 2,
    DeadlockVictim = 3
}

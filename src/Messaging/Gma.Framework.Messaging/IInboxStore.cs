namespace Gma.Framework.Messaging;

public interface IInboxStore
{
    string ModuleName { get; }

    Task<InboxProcessResult> ProcessAsync(
        InboxMessageRecord message,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken);
}

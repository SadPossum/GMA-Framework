namespace Gma.Framework.Messaging;

public interface IEventBus
{
    Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken);
}

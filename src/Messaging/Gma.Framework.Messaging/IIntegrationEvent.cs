namespace Gma.Framework.Messaging;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
    string EventName { get; }
    int Version { get; }
}

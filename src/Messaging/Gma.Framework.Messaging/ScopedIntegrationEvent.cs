namespace Gma.Framework.Messaging;

using Gma.Framework.Naming;

public abstract record ScopedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    protected ScopedIntegrationEvent(
        Guid eventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        string eventName,
        int version)
        : base(eventId, occurredAtUtc, eventName, version)
        => this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));

    public string ScopeId { get; }
}

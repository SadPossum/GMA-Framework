namespace Gma.Framework.Messaging.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;

public abstract class EfOutboxWriter<TDbContext>(
    TDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    string moduleName,
    IEnumerable<IIntegrationEventScopeResolver> scopeResolvers)
    : IOutboxWriter
    where TDbContext : DbContext
{
    private readonly string subjectPrefix = applicationIdentity.Value.EffectiveNamespace;
    private readonly IIntegrationEventScopeResolver[] scopeResolvers =
        scopeResolvers?.ToArray() ??
        throw new ArgumentNullException(nameof(scopeResolvers));

    public string ModuleName { get; } = IntegrationEventNaming.NormalizeModuleName(moduleName);

    public Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        cancellationToken.ThrowIfCancellationRequested();

        IntegrationEventEnvelope envelope = IntegrationEventEnvelopeFactory.Create(
            this.ModuleName,
            integrationEvent,
            this.subjectPrefix,
            this.scopeResolvers);

        dbContext.Set<OutboxMessage>().Add(new OutboxMessage(
            envelope.EventId,
            envelope.Subject,
            envelope.EventType,
            envelope.Version,
            envelope.ScopeId,
            envelope.OccurredAtUtc,
            envelope.Payload,
            clock.UtcNow));

        return Task.CompletedTask;
    }
}

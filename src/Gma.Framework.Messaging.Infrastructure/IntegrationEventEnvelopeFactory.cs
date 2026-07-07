namespace Gma.Framework.Messaging.Infrastructure;

using System.Text.Json;
using Gma.Framework.Messaging;

public static class IntegrationEventEnvelopeFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IntegrationEventEnvelope Create<TEvent>(
        string moduleName,
        TEvent integrationEvent,
        string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix,
        IEnumerable<IIntegrationEventScopeResolver>? scopeResolvers = null)
        where TEvent : IIntegrationEvent
    {
        IntegrationEventMetadata.ValidateForPublishing(integrationEvent);

        string subject = IntegrationEventNaming.CreateSubject(
            subjectPrefix,
            moduleName,
            integrationEvent.EventName,
            integrationEvent.Version);
        string payload = JsonSerializer.Serialize(integrationEvent, JsonOptions);
        string? scopeId = ResolveScopeId(integrationEvent, scopeResolvers);

        return new(
            integrationEvent.EventId,
            subject,
            typeof(TEvent).FullName ?? typeof(TEvent).Name,
            integrationEvent.Version,
            scopeId,
            integrationEvent.OccurredAtUtc,
            payload);
    }

    private static string? ResolveScopeId(
        IIntegrationEvent integrationEvent,
        IEnumerable<IIntegrationEventScopeResolver>? scopeResolvers)
    {
        if (scopeResolvers is null)
        {
            return null;
        }

        string? resolved = null;
        foreach (IIntegrationEventScopeResolver resolver in scopeResolvers)
        {
            string? candidate = MessageScopeIds.NormalizeOptional(
                resolver.ResolveScopeId(integrationEvent),
                nameof(IIntegrationEventScopeResolver.ResolveScopeId));
            if (candidate is null)
            {
                continue;
            }

            if (resolved is not null &&
                !string.Equals(resolved, candidate, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Integration event '{integrationEvent.EventName}' resolved multiple different message scopes.");
            }

            resolved = candidate;
        }

        return resolved;
    }
}

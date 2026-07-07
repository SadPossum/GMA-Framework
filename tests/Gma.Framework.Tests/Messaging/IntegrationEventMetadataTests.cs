namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IntegrationEventMetadataTests
{
    [Theory]
    [MemberData(nameof(InvalidEvents))]
    public void TryGetInvalidReason_classifies_non_retryable_metadata_errors(
        string eventId,
        string eventName,
        int version,
        string expectedReason)
    {
        IIntegrationEvent integrationEvent = CreateEvent(Guid.Parse(eventId), eventName, version);
        bool isInvalid = IntegrationEventMetadata.TryGetInvalidReason(integrationEvent, out string reason);

        Assert.True(isInvalid);
        Assert.Equal(expectedReason, reason);
    }

    [Fact]
    public void TryGetInvalidReason_accepts_valid_metadata()
    {
        bool isInvalid = IntegrationEventMetadata.TryGetInvalidReason(CreateEvent(), out string reason);

        Assert.False(isInvalid);
        Assert.Equal(string.Empty, reason);
    }

    public static TheoryData<string, string, int, string> InvalidEvents()
    {
        return new()
        {
            { EmptyEventId, "item-created", 1, IntegrationEventMetadata.EventIdRequiredReason },
            { ValidEventId, " ", 1, IntegrationEventMetadata.EventNameRequiredReason },
            { ValidEventId, "item created", 1, IntegrationEventMetadata.EventNameInvalidReason },
            { ValidEventId, "item-created", 0, IntegrationEventMetadata.EventVersionInvalidReason }
        };
    }

    private const string EmptyEventId = "00000000-0000-0000-0000-000000000000";
    private const string ValidEventId = "77b9812d-8c63-4cc0-9b8a-68eed57e04a8";

    private static TestIntegrationEvent CreateEvent(
        Guid? eventId = null,
        string eventName = "item-created",
        int version = 1) =>
        new(
            eventId ?? Guid.Parse(ValidEventId),
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero),
            eventName,
            version);

    private sealed record TestIntegrationEvent(
        Guid EventId,
        DateTimeOffset OccurredAtUtc,
        string EventName,
        int Version) : IIntegrationEvent;
}

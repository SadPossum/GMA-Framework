namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IntegrationEventContractGuardTests
{
    [Fact]
    public void Guard_normalizes_common_event_contract_metadata()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTimeOffset occurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(eventId, IntegrationEventContractGuards.RequireId(eventId, "eventId"));
        Assert.Equal(occurredAtUtc, IntegrationEventContractGuards.RequireOccurredAtUtc(occurredAtUtc, "occurredAtUtc"));
        Assert.Equal("member-registered", IntegrationEventContractGuards.NormalizeEventName(" Member-Registered ", "eventName"));
        Assert.Equal(1, IntegrationEventContractGuards.RequireVersion(1, "version"));
        Assert.Equal("text", IntegrationEventContractGuards.NormalizeRequiredText(" text ", 10, "value"));
    }

    [Fact]
    public void Guard_rejects_invalid_common_event_contract_metadata()
    {
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.RequireId(Guid.Empty, "eventId"));
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.RequireOccurredAtUtc(default, "occurredAtUtc"));
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.NormalizeEventName(" ", "eventName"));
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.NormalizeEventName("member registered", "eventName"));
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegrationEventContractGuards.RequireVersion(0, "version"));
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.NormalizeRequiredText(" ", 10, "value"));
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.NormalizeRequiredText("too-long", 3, "value"));
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.NormalizeRequiredText($"bad{char.MinValue}", 10, "value"));
    }

    [Fact]
    public void Guard_validates_numeric_contract_values()
    {
        Assert.Equal(10m, IntegrationEventContractGuards.RequirePositiveDecimal(10m, 18, 2, "price"));
        Assert.Equal(0, IntegrationEventContractGuards.RequireNonNegative(0, "count"));

        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.RequirePositiveDecimal(0, 18, 2, "price"));
        Assert.Throws<ArgumentException>(() => IntegrationEventContractGuards.RequirePositiveDecimal(10.123m, 18, 2, "price"));
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegrationEventContractGuards.RequireNonNegative(-1, "count"));
    }

    [Fact]
    public void Integration_event_base_normalizes_common_metadata()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTimeOffset occurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        TestIntegrationEvent integrationEvent = new(
            eventId,
            occurredAtUtc,
            " Member-Registered ",
            1);

        Assert.Equal(eventId, integrationEvent.EventId);
        Assert.Equal(occurredAtUtc, integrationEvent.OccurredAtUtc);
        Assert.Equal("member-registered", integrationEvent.EventName);
        Assert.Equal(1, integrationEvent.Version);
    }

    [Fact]
    public void Integration_event_base_rejects_invalid_common_metadata()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTimeOffset occurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new TestIntegrationEvent(
            Guid.Empty,
            occurredAtUtc,
            "member-registered",
            1));
        Assert.Throws<ArgumentException>(() => new TestIntegrationEvent(
            eventId,
            default,
            "member-registered",
            1));
        Assert.Throws<ArgumentException>(() => new TestIntegrationEvent(
            eventId,
            occurredAtUtc,
            "member registered",
            1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestIntegrationEvent(
            eventId,
            occurredAtUtc,
            "member-registered",
            0));
    }

    [Fact]
    public void Guard_maps_undefined_enum_values_to_unknown_default()
    {
        Assert.Equal(TestStatus.Active, IntegrationEventContractGuards.NormalizeDefinedOrUnknown(TestStatus.Active));
        Assert.Equal(TestStatus.Unknown, IntegrationEventContractGuards.NormalizeDefinedOrUnknown((TestStatus)999));
    }

    private sealed record TestIntegrationEvent : IntegrationEvent
    {
        public TestIntegrationEvent(
            Guid eventId,
            DateTimeOffset occurredAtUtc,
            string eventName,
            int version)
            : base(eventId, occurredAtUtc, eventName, version)
        {
        }
    }

    private enum TestStatus
    {
        Unknown = 0,
        Active = 1
    }
}

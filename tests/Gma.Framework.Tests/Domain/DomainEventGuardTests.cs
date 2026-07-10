namespace Gma.Framework.Tests;

using Gma.Framework.Naming;
using Gma.Framework.Domain;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DomainEventGuardTests
{
    [Fact]
    public void Guard_normalizes_common_domain_event_metadata()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTimeOffset occurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(eventId, DomainEventGuards.RequireId(eventId, "eventId"));
        Assert.Equal(occurredAtUtc, DomainEventGuards.RequireOccurredAtUtc(occurredAtUtc, "occurredAtUtc"));
        Assert.Equal("tenant-a", DomainEventGuards.NormalizeScopeId(" tenant-a ", "scopeId"));
        Assert.Equal("value", DomainEventGuards.NormalizeRequiredText(" value ", 10, "value"));
    }

    [Fact]
    public void Guard_rejects_invalid_common_domain_event_metadata()
    {
        Assert.Throws<ArgumentException>(() => DomainEventGuards.RequireId(Guid.Empty, "eventId"));
        Assert.Throws<ArgumentException>(() => DomainEventGuards.RequireOccurredAtUtc(default, "occurredAtUtc"));
        Assert.Throws<ArgumentException>(() => DomainEventGuards.NormalizeScopeId(" ", "scopeId"));
        Assert.Throws<ArgumentException>(() => DomainEventGuards.NormalizeScopeId(new string('x', ScopeIds.MaxLength + 1), "scopeId"));
        Assert.Throws<ArgumentException>(() => DomainEventGuards.NormalizeRequiredText(" ", 10, "value"));
        Assert.Throws<ArgumentException>(() => DomainEventGuards.NormalizeRequiredText("too-long", 3, "value"));
        Assert.Throws<ArgumentException>(() => DomainEventGuards.NormalizeRequiredText($"bad{char.MinValue}", 10, "value"));
    }

    [Fact]
    public void Guard_validates_numeric_domain_event_values()
    {
        Assert.Equal(10m, DomainEventGuards.RequirePositiveDecimal(10m, 18, 2, "price"));
        Assert.Equal(1, DomainEventGuards.RequirePositive(1, "count"));

        Assert.Throws<ArgumentException>(() => DomainEventGuards.RequirePositiveDecimal(0, 18, 2, "price"));
        Assert.Throws<ArgumentException>(() => DomainEventGuards.RequirePositiveDecimal(10.123m, 18, 2, "price"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainEventGuards.RequirePositive(0, "count"));
    }

    [Fact]
    public void Guard_maps_undefined_enum_values_to_unknown_default()
    {
        Assert.Equal(TestStatus.Active, DomainEventGuards.NormalizeDefinedOrUnknown(TestStatus.Active));
        Assert.Equal(TestStatus.Unknown, DomainEventGuards.NormalizeDefinedOrUnknown((TestStatus)999));
    }

    private enum TestStatus
    {
        Unknown = 0,
        Active = 1
    }
}

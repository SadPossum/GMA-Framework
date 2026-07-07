namespace Gma.Framework.Tests;

using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DomainEventTests
{
    [Fact]
    public void Aggregate_root_collects_and_clears_domain_events()
    {
        TestAggregate aggregate = new(Guid.NewGuid());
        aggregate.Touch();

        Assert.Single(aggregate.DomainEvents);

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Aggregate_root_domain_events_are_read_only_to_callers()
    {
        TestAggregate aggregate = new(Guid.NewGuid());
        aggregate.Touch();

        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<IDomainEvent>)aggregate.DomainEvents).Add(
                new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow)));
        Assert.Single(aggregate.DomainEvents);
    }

    [Fact]
    public void Aggregate_root_rejects_null_domain_events()
    {
        TestAggregate aggregate = new(Guid.NewGuid());

        Assert.Throws<ArgumentNullException>(aggregate.TouchWithNull);
    }

    [Fact]
    public void Domain_event_base_normalizes_common_metadata()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTimeOffset occurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        TestDomainEvent domainEvent = new(eventId, occurredAtUtc);

        Assert.Equal(eventId, domainEvent.EventId);
        Assert.Equal(occurredAtUtc, domainEvent.OccurredAtUtc);
    }

    [Fact]
    public void Tenant_domain_event_base_normalizes_tenant_id()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTimeOffset occurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        TestTenantDomainEvent domainEvent = new(eventId, occurredAtUtc, " tenant-a ");

        Assert.Equal(eventId, domainEvent.EventId);
        Assert.Equal(occurredAtUtc, domainEvent.OccurredAtUtc);
        Assert.Equal("tenant-a", domainEvent.TenantId);
    }

    [Fact]
    public void Domain_event_bases_reject_invalid_common_metadata()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTimeOffset occurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new TestDomainEvent(Guid.Empty, occurredAtUtc));
        Assert.Throws<ArgumentException>(() => new TestDomainEvent(eventId, default));
        Assert.Throws<ArgumentException>(() => new TestTenantDomainEvent(eventId, occurredAtUtc, " "));
    }

    private sealed class TestAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Touch() => this.RaiseDomainEvent(new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow));
        public void TouchWithNull() => this.RaiseDomainEvent(null!);
    }

    private sealed record TestDomainEvent : DomainEvent
    {
        public TestDomainEvent(Guid eventId, DateTimeOffset occurredAtUtc)
            : base(eventId, occurredAtUtc)
        {
        }
    }

    private sealed record TestTenantDomainEvent : TenantDomainEvent
    {
        public TestTenantDomainEvent(Guid eventId, DateTimeOffset occurredAtUtc, string tenantId)
            : base(eventId, occurredAtUtc, tenantId)
        {
        }
    }
}

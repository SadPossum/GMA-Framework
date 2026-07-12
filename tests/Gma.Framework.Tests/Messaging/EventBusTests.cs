namespace Gma.Framework.Tests;

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EventBusTests
{
    [Fact]
    public async Task Null_event_bus_rejects_null_message_before_missing_adapter_error()
    {
        var eventBus = new NullEventBus();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            eventBus.PublishAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Nats_event_bus_rejects_null_message_before_using_connection()
    {
        INatsConnection connection = CreateUnusedNatsConnection();
        var eventBus = new NatsJetStreamEventBus(
            connection,
            CreateStreamManager(connection, new NatsJetStreamOptions()),
            NullLogger<NatsJetStreamEventBus>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            eventBus.PublishAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Nats_stream_manager_rejects_null_connection_and_invalid_stream_options()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new NatsJetStreamStreamManager(
                connection: null!,
                Options.Create(new NatsJetStreamOptions()),
                Options.Create(new ApplicationIdentityOptions()),
                NullLogger<NatsJetStreamStreamManager>.Instance));

        Assert.True(new NatsJetStreamOptionsValidator()
            .Validate(null, new NatsJetStreamOptions { StreamName = "GMA.EVENTS" })
            .Failed);
    }

    [Fact]
    public async Task Null_event_bus_reports_missing_adapter_for_real_messages()
    {
        var eventBus = new NullEventBus();
        OutboxMessageRecord message = new(
            Guid.NewGuid(),
            "gma.auth.member-registered.v1",
            "Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent",
            1,
            "tenant-a",
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero),
            "{}");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            eventBus.PublishAsync(message, CancellationToken.None));

        Assert.Contains("No integration event bus is configured", exception.Message, StringComparison.Ordinal);
    }

    private static INatsConnection CreateUnusedNatsConnection() =>
        DispatchProxy.Create<INatsConnection, UnusedNatsConnectionProxy>();

    private static NatsJetStreamStreamManager CreateStreamManager(
        INatsConnection connection,
        NatsJetStreamOptions options) =>
        new(
            connection,
            Options.Create(options),
            Options.Create(new ApplicationIdentityOptions()),
            NullLogger<NatsJetStreamStreamManager>.Instance);

    public class UnusedNatsConnectionProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("The NATS connection should not be used by this test.");
    }
}

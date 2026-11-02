namespace Gma.Framework.Tests.Messaging;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.Runtime;
using Gma.Framework.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Xunit;

[Trait("Category", "Integration")]
[Trait("Category", "Docker")]
public sealed class NatsJetStreamEventBusProviderTests
{
    private const int NatsPort = 4222;

    [DockerFact]
    public async Task Dedupe_is_subject_scoped_and_expires_with_the_broker_window()
    {
        await using IContainer nats = CreateNatsContainer();
        await nats.StartAsync();

        TimeSpan duplicateWindow = TimeSpan.FromSeconds(1);
        string streamName = $"GMA_DEDUPE_{Guid.NewGuid():N}".ToUpperInvariant();
        await using NatsConnection connection = new(new NatsOpts
        {
            Url = GetNatsConnectionString(nats),
        });
        using NatsJetStreamStreamManager manager = new(
            connection,
            Options.Create(new NatsJetStreamOptions
            {
                StreamName = streamName,
                Storage = NatsStreamStorage.Memory,
                DuplicateWindow = duplicateWindow,
            }),
            Options.Create(new ApplicationIdentityOptions { Namespace = "gma" }),
            NullLogger<NatsJetStreamStreamManager>.Instance);
        using NatsJetStreamEventBus eventBus = new(
            connection,
            manager,
            NullLogger<NatsJetStreamEventBus>.Instance);
        Guid sharedMessageId = Guid.NewGuid();
        OutboxMessageRecord created = CreateMessage(
            sharedMessageId,
            "gma.catalog.item-created.v1",
            "Gma.Framework.Tests.ItemCreatedIntegrationEvent",
            "created");
        OutboxMessageRecord updated = CreateMessage(
            sharedMessageId,
            "gma.catalog.item-updated.v1",
            "Gma.Framework.Tests.ItemUpdatedIntegrationEvent",
            "updated");

        await eventBus.PublishAsync(created, CancellationToken.None);
        await eventBus.PublishAsync(created, CancellationToken.None);
        Assert.Equal(1L, await GetStoredMessageCountAsync(connection, streamName));

        await eventBus.PublishAsync(updated, CancellationToken.None);
        Assert.Equal(2L, await GetStoredMessageCountAsync(connection, streamName));

        await Task.Delay(duplicateWindow + TimeSpan.FromSeconds(1));
        await eventBus.PublishAsync(created, CancellationToken.None);
        Assert.Equal(3L, await GetStoredMessageCountAsync(connection, streamName));
    }

    private static IContainer CreateNatsContainer() =>
        new ContainerBuilder("nats:2.10-alpine")
            .WithPortBinding(NatsPort, assignRandomHostPort: true)
            .WithCommand("-js")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(NatsPort))
            .Build();

    private static string GetNatsConnectionString(IContainer container) =>
        $"nats://localhost:{container.GetMappedPublicPort(NatsPort)}";

    private static async Task<long> GetStoredMessageCountAsync(
        INatsConnection connection,
        string streamName)
    {
        NatsJSContext jetStream = new(connection);
        INatsJSStream stream = await jetStream.GetStreamAsync(
            streamName,
            cancellationToken: CancellationToken.None);
        return stream.Info.State.Messages;
    }

    private static OutboxMessageRecord CreateMessage(
        Guid id,
        string subject,
        string eventType,
        string suffix) =>
        new(
            id,
            subject,
            eventType,
            1,
            "tenant-a",
            DateTimeOffset.UtcNow,
            $$"""{"suffix":"{{suffix}}"}""");
}

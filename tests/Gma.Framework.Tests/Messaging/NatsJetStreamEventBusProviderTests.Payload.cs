namespace Gma.Framework.Tests.Messaging;

using DotNet.Testcontainers.Containers;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.Runtime;
using Gma.Framework.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Xunit;

public sealed partial class NatsJetStreamEventBusProviderTests
{
    [DockerFact]
    public async Task Published_utf8_payload_survives_fetch_negative_acknowledgement_and_redelivery()
    {
        // Cover the Framework provider's existing server and the product rehearsal server.
        foreach (string image in new[] { "nats:2.10-alpine", "nats:2.11-alpine" })
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
            CancellationToken cancellationToken = timeout.Token;
            await using IContainer nats = CreateNatsContainer(image);
            await nats.StartAsync(cancellationToken);
            await using NatsConnection connection = new(new NatsOpts { Url = GetNatsConnectionString(nats) });
            string streamName = $"GMA_PAYLOAD_{Guid.NewGuid():N}".ToUpperInvariant();
            using NatsJetStreamStreamManager manager = new(
                connection,
                Options.Create(new NatsJetStreamOptions { StreamName = streamName, Storage = NatsStreamStorage.Memory }),
                Options.Create(new ApplicationIdentityOptions { Namespace = "gma" }),
                NullLogger<NatsJetStreamStreamManager>.Instance);
            using NatsJetStreamEventBus eventBus = new(connection, manager, NullLogger<NatsJetStreamEventBus>.Instance);
            Guid eventId = Guid.NewGuid();
            string payload = $$"""{"eventId":"{{eventId:D}}","scopeId":"tenant-a","text":"héllo 東京","version":1}""";
            OutboxMessageRecord published = new(eventId, "gma.catalog.item-created.v1",
                "Gma.Framework.Tests.ItemCreatedIntegrationEvent", 1, "tenant-a", DateTimeOffset.UtcNow, payload);
            await eventBus.PublishAsync(published, cancellationToken);

            NatsJSContext jetStream = new(connection);
            INatsJSConsumer consumer = await jetStream.CreateOrUpdateConsumerAsync(
                streamName,
                new ConsumerConfig("payload-reader")
                {
                    FilterSubject = published.Subject,
                    AckWait = TimeSpan.FromSeconds(5),
                    MaxDeliver = 3,
                    MaxAckPending = 1
                },
                cancellationToken);
            INatsJSMsg<string> first = await FetchOneAsync(consumer, cancellationToken);
            Assert.Equal(payload, first.Data);
            Assert.True(first.Metadata is { NumDelivered: 1 });
            await first.NakAsync(new AckOpts { NakDelay = TimeSpan.FromMilliseconds(50) }, cancellationToken);

            INatsJSMsg<string> redelivered = await FetchOneAsync(consumer, cancellationToken);
            Assert.Equal(payload, redelivered.Data);
            Assert.True(redelivered.Metadata is { NumDelivered: 2 });
            Assert.Equal(first.Metadata?.Sequence.Stream, redelivered.Metadata?.Sequence.Stream);
            // Broker state is observable only after the server confirms this acknowledgment.
            await redelivered.AckAsync(new AckOpts { DoubleAck = true }, cancellationToken);
            await consumer.RefreshAsync(cancellationToken);
            Assert.Equal(0, consumer.Info.NumAckPending);
            Assert.Equal(0UL, consumer.Info.NumPending);
        }
    }

    private static async Task<INatsJSMsg<string>> FetchOneAsync(INatsJSConsumer consumer, CancellationToken cancellationToken)
    {
        await foreach (INatsJSMsg<string> message in consumer.FetchAsync(
                           new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(2) },
                           NatsDefaultSerializer<string>.Default,
                           cancellationToken))
        {
            return message;
        }

        throw new InvalidOperationException("The real JetStream consumer did not return the expected retained message.");
    }
}

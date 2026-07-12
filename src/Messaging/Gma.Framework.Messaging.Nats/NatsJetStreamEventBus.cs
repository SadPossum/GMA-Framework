namespace Gma.Framework.Messaging.Nats;

using System.Text;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Gma.Framework.Messaging;

public sealed class NatsJetStreamEventBus(
    INatsConnection connection,
    NatsJetStreamStreamManager streamManager,
    ILogger<NatsJetStreamEventBus> logger) : IEventBus, IDisposable
{
    private readonly INatsConnection connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly NatsJetStreamStreamManager streamManager = streamManager ??
        throw new ArgumentNullException(nameof(streamManager));

    public async Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        NatsJSContext jetStream = new(this.connection);
        await this.streamManager.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        byte[] payload = Encoding.UTF8.GetBytes(message.Payload);
        NatsJSPubOpts publishOptions = new()
        {
            MsgId = CreateMessageId(message.Id)
        };
        PubAckResponse ack = await jetStream
            .PublishAsync(message.Subject, payload, opts: publishOptions, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (ack.Duplicate)
        {
            this.LogDuplicatePublish(message.Id, message.Subject);
            return;
        }

        ack.EnsureSuccess();
        this.LogPublished(message.Id, message.Subject);
    }

    private static string CreateMessageId(Guid messageId) =>
        messageId.ToString("N");

    public void Dispose() { }

    private void LogPublished(Guid eventId, string subject)
    {
        try
        {
            logger.LogInformation("Published integration event {EventId} to {Subject}", eventId, subject);
        }
        catch (Exception)
        {
            // A successful broker ack must stay successful even when observability is unavailable.
        }
    }

    private void LogDuplicatePublish(Guid eventId, string subject)
    {
        try
        {
            logger.LogInformation(
                "NATS JetStream ignored duplicate integration event {EventId} on {Subject}",
                eventId,
                subject);
        }
        catch (Exception)
        {
            // A duplicate ack still means the broker has already accepted this outbox message.
        }
    }
}

namespace Gma.Framework.Messaging.Nats;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

#pragma warning disable IDE0290 // Explicit constructor selects the shared-manager DI path over the compatibility overload.
public sealed class NatsJetStreamEventBus : IEventBus, IDisposable
{
    private readonly INatsConnection connection;
    private readonly NatsJetStreamStreamManager streamManager;
    private readonly ILogger<NatsJetStreamEventBus> logger;
    private readonly NatsJetStreamStreamManager? ownedStreamManager;

    public NatsJetStreamEventBus(
        INatsConnection connection,
        NatsJetStreamStreamManager streamManager,
        ILogger<NatsJetStreamEventBus> logger)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public NatsJetStreamEventBus(
        INatsConnection connection,
        IOptions<NatsJetStreamOptions> options,
        IOptions<ApplicationIdentityOptions> applicationIdentity,
        ILogger<NatsJetStreamEventBus> logger)
        : this(
            connection,
            new NatsJetStreamStreamManager(
                connection,
                options,
                applicationIdentity,
                NullLogger<NatsJetStreamStreamManager>.Instance),
            logger)
        => this.ownedStreamManager = this.streamManager;

    public async Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        NatsJSContext jetStream = new(this.connection);
        await this.streamManager.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        byte[] payload = Encoding.UTF8.GetBytes(message.Payload);
        NatsJSPubOpts publishOptions = new()
        {
            MsgId = CreateMessageId(message.Subject, message.Id)
        };
        PubAckResponse ack = await jetStream
            .PublishAsync(message.Subject, payload, opts: publishOptions, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (ack.Duplicate)
        {
            this.LogDuplicatePublish(message.Subject);
            return;
        }

        ack.EnsureSuccess();
        this.LogPublished(message.Subject);
    }

    internal static string CreateMessageId(string subject, Guid messageId)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("messageId must not be empty.", nameof(messageId));
        }

        string normalizedSubject = IntegrationEventNaming.NormalizeSubject(subject);
        byte[] identity = Encoding.UTF8.GetBytes(
            string.Concat(normalizedSubject, "\0", messageId.ToString("N")));
        return Convert.ToHexStringLower(SHA256.HashData(identity));
    }

    public void Dispose() => this.ownedStreamManager?.Dispose();

    private void LogPublished(string subject)
    {
        try
        {
            this.logger.LogInformation("Published integration event to {Subject}", subject);
        }
        catch (Exception)
        {
            // A successful broker ack must stay successful even when observability is unavailable.
        }
    }

    private void LogDuplicatePublish(string subject)
    {
        try
        {
            this.logger.LogInformation(
                "NATS JetStream ignored a duplicate integration event on {Subject}",
                subject);
        }
        catch (Exception)
        {
            // A duplicate ack still means the broker has already accepted this outbox message.
        }
    }
}
#pragma warning restore IDE0290

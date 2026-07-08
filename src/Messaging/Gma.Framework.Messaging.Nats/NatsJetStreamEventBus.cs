namespace Gma.Framework.Messaging.Nats;

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime;

public sealed class NatsJetStreamEventBus(
    INatsConnection connection,
    IOptions<NatsJetStreamOptions> options,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    ILogger<NatsJetStreamEventBus> logger) : IEventBus, IDisposable
{
    private readonly SemaphoreSlim streamSetupLock = new(1, 1);
    private readonly string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
    private readonly string streamName = (options ?? throw new ArgumentNullException(nameof(options)))
        .Value
        .EffectiveStreamName(applicationIdentity.Value.EffectiveNamespace);
    private readonly INatsConnection connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private volatile bool streamReady;

    public async Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        NatsJSContext jetStream = new(this.connection);
        await this.EnsureStreamAsync(jetStream, cancellationToken).ConfigureAwait(false);

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

    private async Task EnsureStreamAsync(NatsJSContext jetStream, CancellationToken cancellationToken)
    {
        if (this.streamReady)
        {
            return;
        }

        await this.streamSetupLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (this.streamReady)
            {
                return;
            }

            await jetStream.CreateStreamAsync(
                    new StreamConfig(this.streamName, [NatsJetStreamOptions.CreateSubjectWildcard(this.applicationNamespace)]),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            this.streamReady = true;
        }
        catch (NatsJSApiException exception) when (IsAlreadyExists(exception))
        {
            this.LogStreamAlreadyExists(exception);
            this.streamReady = true;
        }
        finally
        {
            this.streamSetupLock.Release();
        }
    }

    private static bool IsAlreadyExists(NatsJSApiException exception)
    {
        string description = exception.Error.Description ?? string.Empty;

        return description.Contains("already", StringComparison.OrdinalIgnoreCase) &&
               (description.Contains("exist", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("in use", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        this.streamSetupLock.Dispose();
    }

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

    private void LogStreamAlreadyExists(NatsJSApiException exception)
    {
        try
        {
            logger.LogDebug(exception, "NATS stream {StreamName} already exists.", this.streamName);
        }
        catch (Exception)
        {
            // Existing stream setup should not fail because the debug logger failed.
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

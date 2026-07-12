namespace Gma.Framework.Messaging.Nats;

using Gma.Framework.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

public sealed class NatsJetStreamStreamManager(
    INatsConnection connection,
    IOptions<NatsJetStreamOptions> options,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    ILogger<NatsJetStreamStreamManager> logger) : IDisposable
{
    private readonly SemaphoreSlim setupLock = new(1, 1);
    private readonly INatsConnection connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly NatsJetStreamOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly string applicationNamespace = applicationIdentity?.Value.EffectiveNamespace ??
        throw new ArgumentNullException(nameof(applicationIdentity));
    private readonly string streamName = options.Value.EffectiveStreamName(
        applicationIdentity.Value.EffectiveNamespace);
    private volatile bool ready;

    public string StreamName => this.streamName;

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (this.ready)
        {
            return;
        }

        await this.setupLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.ready)
            {
                return;
            }

            NatsJSContext jetStream = new(this.connection);
            INatsJSStream stream = this.options.ManagementMode == NatsStreamManagementMode.Managed
                ? await jetStream.CreateOrUpdateStreamAsync(this.CreateExpectedConfig(), cancellationToken)
                    .ConfigureAwait(false)
                : await jetStream.GetStreamAsync(this.StreamName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            this.ValidateStream(stream.Info.Config);
            this.ready = true;
            this.LogReady();
        }
        finally
        {
            this.setupLock.Release();
        }
    }

    public void Dispose() => this.setupLock.Dispose();

    private StreamConfig CreateExpectedConfig() =>
        new(this.StreamName, [NatsJetStreamOptions.CreateSubjectWildcard(this.applicationNamespace)])
        {
            MaxAge = this.options.MaxAge,
            MaxBytes = this.options.MaxBytes,
            MaxMsgs = this.options.MaxMessages,
            NumReplicas = this.options.Replicas,
            Storage = this.options.Storage == NatsStreamStorage.File
                ? StreamConfigStorage.File
                : StreamConfigStorage.Memory,
            Discard = StreamConfigDiscard.Old,
            DuplicateWindow = this.options.DuplicateWindow,
        };

    private void ValidateStream(StreamConfig actual)
    {
        StreamConfig expected = this.CreateExpectedConfig();
        List<string> mismatches = [];
        string expectedSubject = NatsJetStreamOptions.CreateSubjectWildcard(this.applicationNamespace);

        if (actual.Subjects is null || !actual.Subjects.Contains(expectedSubject, StringComparer.Ordinal))
        {
            mismatches.Add($"subjects must include '{expectedSubject}'");
        }

        AddMismatch(mismatches, nameof(StreamConfig.MaxAge), expected.MaxAge, actual.MaxAge);
        AddMismatch(mismatches, nameof(StreamConfig.MaxBytes), expected.MaxBytes, actual.MaxBytes);
        AddMismatch(mismatches, nameof(StreamConfig.MaxMsgs), expected.MaxMsgs, actual.MaxMsgs);
        AddMismatch(mismatches, nameof(StreamConfig.NumReplicas), expected.NumReplicas, actual.NumReplicas);
        AddMismatch(mismatches, nameof(StreamConfig.Storage), expected.Storage, actual.Storage);
        AddMismatch(
            mismatches,
            nameof(StreamConfig.DuplicateWindow),
            expected.DuplicateWindow,
            actual.DuplicateWindow);

        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                $"NATS JetStream stream '{this.StreamName}' does not match configured safety limits: {string.Join("; ", mismatches)}.");
        }
    }

    private static void AddMismatch<T>(List<string> mismatches, string name, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            mismatches.Add($"{name} expected '{expected}' but was '{actual}'");
        }
    }

    private void LogReady()
    {
        try
        {
            logger.LogInformation(
                "NATS JetStream stream {StreamName} is ready in {ManagementMode} mode.",
                this.StreamName,
                this.options.ManagementMode);
        }
        catch (Exception)
        {
            // Stream readiness must not fail because observability is unavailable.
        }
    }
}

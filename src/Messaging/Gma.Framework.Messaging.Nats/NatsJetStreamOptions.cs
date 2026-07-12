namespace Gma.Framework.Messaging.Nats;

using Gma.Framework.Naming;

public sealed class NatsJetStreamOptions
{
    public const string SectionName = "NatsJetStream";

    public bool Enabled { get; set; }
    public string? StreamName { get; set; }
    public NatsStreamManagementMode ManagementMode { get; set; } = NatsStreamManagementMode.Managed;
    public NatsStreamStorage Storage { get; set; } = NatsStreamStorage.File;
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);
    public long MaxBytes { get; set; } = 1_073_741_824;
    public long MaxMessages { get; set; } = 10_000_000;
    public int Replicas { get; set; } = 1;
    public TimeSpan DuplicateWindow { get; set; } = TimeSpan.FromMinutes(2);

    public string EffectiveStreamName(string applicationNamespace) =>
        string.IsNullOrWhiteSpace(this.StreamName)
            ? ApplicationNamespaces.CreateStreamName(applicationNamespace)
            : NatsStreamNames.Normalize(this.StreamName);

    public static string CreateSubjectWildcard(string applicationNamespace) =>
        ApplicationNamespaces.CreateWildcardSubject(applicationNamespace);
}

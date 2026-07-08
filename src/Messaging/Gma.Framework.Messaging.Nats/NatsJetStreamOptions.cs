namespace Gma.Framework.Messaging.Nats;

using Gma.Framework.Naming;

public sealed class NatsJetStreamOptions
{
    public const string SectionName = "NatsJetStream";

    public bool Enabled { get; set; }
    public string? StreamName { get; set; }

    public string EffectiveStreamName(string applicationNamespace) =>
        string.IsNullOrWhiteSpace(this.StreamName)
            ? ApplicationNamespaces.CreateStreamName(applicationNamespace)
            : NatsStreamNames.Normalize(this.StreamName);

    public static string CreateSubjectWildcard(string applicationNamespace) =>
        ApplicationNamespaces.CreateWildcardSubject(applicationNamespace);
}

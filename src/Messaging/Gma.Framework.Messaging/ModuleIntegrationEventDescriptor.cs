namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

public sealed record ModuleIntegrationEventDescriptor : IModuleMetadataProvider
{
    public ModuleIntegrationEventDescriptor(
        string eventType,
        string subject,
        int version,
        IReadOnlyList<ModuleMetadataItem>? metadata = null)
    {
        this.EventType = IntegrationEventNaming.NormalizeEventName(eventType, nameof(eventType));
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        IntegrationEventSubject parsedSubject = IntegrationEventNaming.ParseSubject(subject, nameof(subject));
        this.SubjectPrefix = parsedSubject.SubjectPrefix;
        this.ModuleName = parsedSubject.ModuleName;
        this.Subject = parsedSubject.CreateSubject();
        this.Version = version;
        this.Metadata = ModuleMetadataItems.Create(metadata);
    }

    public string EventType { get; }
    public string SubjectPrefix { get; }
    public string ModuleName { get; }
    public string Subject { get; }
    public int Version { get; }
    public ModuleMetadataItems Metadata { get; }

    public string CreateSubject(string subjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, this.ModuleName, this.EventType, this.Version);
}

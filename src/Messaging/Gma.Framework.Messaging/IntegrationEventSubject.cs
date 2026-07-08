namespace Gma.Framework.Messaging;

public sealed record IntegrationEventSubject(
    string SubjectPrefix,
    string ModuleName,
    string EventName,
    int Version)
{
    public string SubjectPrefix { get; } = IntegrationEventNaming.NormalizeSubjectPrefix(SubjectPrefix);
    public string ModuleName { get; } = IntegrationEventNaming.NormalizeModuleName(ModuleName);
    public string EventName { get; } = IntegrationEventNaming.NormalizeEventName(EventName);
    public int Version { get; } = Version >= 1
        ? Version
        : throw new ArgumentOutOfRangeException(nameof(Version), Version, "Version must be positive.");

    public string CreateSubject(string? subjectPrefix = null) =>
        IntegrationEventNaming.CreateSubject(
            subjectPrefix ?? this.SubjectPrefix,
            this.ModuleName,
            this.EventName,
            this.Version);

    public override string ToString() => this.CreateSubject();
}

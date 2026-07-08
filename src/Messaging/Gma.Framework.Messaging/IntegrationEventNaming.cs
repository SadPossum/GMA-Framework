namespace Gma.Framework.Messaging;

using System.Globalization;
using Gma.Framework.Naming;

public static class IntegrationEventNaming
{
    public const string DefaultSubjectPrefix = ApplicationNamespaces.Default;

    public static string NormalizeSubjectPrefix(string subjectPrefix, string parameterName = "subjectPrefix") =>
        ApplicationNamespaces.Normalize(subjectPrefix, parameterName);

    public static string NormalizeModuleName(string moduleName, string parameterName = "moduleName") =>
        SharedModuleNames.Normalize(moduleName, parameterName);

    public static string NormalizeEventName(string eventName, string parameterName = "eventName") =>
        SharedNameSegments.NormalizeKebabSegment(eventName, "event name", parameterName);

    public static bool TryNormalizeEventName(string? eventName, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = eventName.Trim().ToLowerInvariant();
        return SharedNameSegments.IsKebabSegment(normalized);
    }

    public static string NormalizeHandlerName(string handlerName, string parameterName = "handlerName") =>
        SharedNameSegments.NormalizeKebabSegment(handlerName, "handler name", parameterName);

    public static string CreateSubject(
        string subjectPrefix,
        string moduleName,
        string eventName,
        int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{NormalizeSubjectPrefix(subjectPrefix)}.{NormalizeModuleName(moduleName)}.{NormalizeEventName(eventName)}.v{version}");
    }

    public static string CreateSubject(
        string moduleName,
        string eventName,
        int version) =>
        CreateSubject(DefaultSubjectPrefix, moduleName, eventName, version);

    public static string NormalizeSubject(string subject, string parameterName = "subject")
    {
        return ParseSubject(subject, parameterName).CreateSubject();
    }

    public static IntegrationEventSubject ParseSubject(string subject, string parameterName = "subject")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject, parameterName);

        string normalized = subject.Trim().ToLowerInvariant();
        string[] parts = normalized.Split('.');

        if (parts.Length != 4 ||
            !parts[3].StartsWith('v') ||
            !int.TryParse(parts[3][1..], NumberStyles.None, CultureInfo.InvariantCulture, out int version) ||
            parts[3] != string.Create(CultureInfo.InvariantCulture, $"v{version}"))
        {
            throw new ArgumentException(
                $"{parameterName} must use the <namespace>.<module>.<event>.v<version> integration-event subject shape.",
                parameterName);
        }

        return new IntegrationEventSubject(parts[0], parts[1], parts[2], version);
    }
}

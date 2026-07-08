namespace Gma.Framework.Observability;

using Gma.Framework.Naming;

public static class ObservabilityMeterNames
{
    public const string Application = ApplicationNamespaces.Default + ".application";
    public const string Caching = ApplicationNamespaces.Default + ".caching";
    public const string Messaging = ApplicationNamespaces.Default + ".messaging";
    public const string Notifications = ApplicationNamespaces.Default + ".notifications";
    public const string Tasks = ApplicationNamespaces.Default + ".tasks";

    public static string ApplicationFor(string applicationNamespace) => Create(applicationNamespace, "application");

    public static string CachingFor(string applicationNamespace) => Create(applicationNamespace, "caching");

    public static string MessagingFor(string applicationNamespace) => Create(applicationNamespace, "messaging");

    public static string NotificationsFor(string applicationNamespace) => Create(applicationNamespace, "notifications");

    public static string TasksFor(string applicationNamespace) => Create(applicationNamespace, "tasks");

    private static string Create(string applicationNamespace, string area) =>
        $"{ApplicationNamespaces.Normalize(applicationNamespace)}.{area}";
}

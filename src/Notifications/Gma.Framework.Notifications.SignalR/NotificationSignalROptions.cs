namespace Gma.Framework.Notifications.SignalR;

public sealed class NotificationSignalROptions
{
    public const string SectionName = "Notifications:SignalR";
    public const string DefaultHubPath = "/hubs/notifications";
    public const string DefaultClientMethodName = "notification";
    public const string DefaultAccessTokenQueryParameter = "access_token";

    public bool Enabled { get; set; } = true;
    public string HubPath { get; set; } = DefaultHubPath;
    public string ClientMethodName { get; set; } = DefaultClientMethodName;
    public string AccessTokenQueryParameter { get; set; } = DefaultAccessTokenQueryParameter;
}

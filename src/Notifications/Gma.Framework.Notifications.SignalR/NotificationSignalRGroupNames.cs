namespace Gma.Framework.Notifications.SignalR;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Naming;

internal static class NotificationSignalRGroupNames
{
    public static string ForUser(string applicationNamespace, string scopeId, string userId)
    {
        string normalizedNamespace = ApplicationNamespaces.Normalize(applicationNamespace);
        string input = $"{scopeId}\u001f{userId}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        return $"{normalizedNamespace}:notifications:user:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

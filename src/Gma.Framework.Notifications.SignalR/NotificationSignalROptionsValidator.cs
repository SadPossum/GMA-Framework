namespace Gma.Framework.Notifications.SignalR;

using Microsoft.Extensions.Options;
using Gma.Framework.Security;

internal sealed class NotificationSignalROptionsValidator : IValidateOptions<NotificationSignalROptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationSignalROptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HubPath) ||
            !options.HubPath.StartsWith('/') ||
            options.HubPath.Any(char.IsWhiteSpace) ||
            options.HubPath.Any(char.IsControl))
        {
            return ValidateOptionsResult.Fail(
                "Notifications:SignalR:HubPath must be an absolute path without whitespace or control characters.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientMethodName) ||
            options.ClientMethodName.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return ValidateOptionsResult.Fail(
                "Notifications:SignalR:ClientMethodName is required and cannot contain whitespace or control characters.");
        }

        if (!ApplicationClaimNames.IsValidClaimName(options.AccessTokenQueryParameter))
        {
            return ValidateOptionsResult.Fail(
                "Notifications:SignalR:AccessTokenQueryParameter is required and cannot contain whitespace or control characters.");
        }

        return ValidateOptionsResult.Success;
    }
}

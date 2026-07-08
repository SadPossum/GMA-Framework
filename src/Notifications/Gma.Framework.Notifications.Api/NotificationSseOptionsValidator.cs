namespace Gma.Framework.Notifications.Api;

using Microsoft.Extensions.Options;

internal sealed class NotificationSseOptionsValidator : IValidateOptions<NotificationSseOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationSseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.StreamPath) ||
            !options.StreamPath.StartsWith('/') ||
            options.StreamPath.Any(char.IsWhiteSpace) ||
            options.StreamPath.Any(char.IsControl))
        {
            return ValidateOptionsResult.Fail(
                "Notifications:Sse:StreamPath must be an absolute path without whitespace or control characters.");
        }

        if (string.IsNullOrWhiteSpace(options.NotificationEventType) ||
            options.NotificationEventType.Any(character => character is '\r' or '\n' || char.IsControl(character)))
        {
            return ValidateOptionsResult.Fail(
                "Notifications:Sse:NotificationEventType is required and cannot contain line breaks or control characters.");
        }

        if (options.HeartbeatInterval < TimeSpan.FromSeconds(1) ||
            options.HeartbeatInterval > TimeSpan.FromMinutes(5))
        {
            return ValidateOptionsResult.Fail(
                "Notifications:Sse:HeartbeatInterval must be between 1 second and 5 minutes.");
        }

        return ValidateOptionsResult.Success;
    }
}

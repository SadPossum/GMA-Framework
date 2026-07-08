namespace Gma.Framework.Notifications.Infrastructure;

using Microsoft.Extensions.Options;
using Gma.Framework.Notifications;

internal sealed class NotificationsOptionsValidator : IValidateOptions<NotificationsOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationsOptions options)
    {
        if (options.SubscriberQueueCapacity is <= 0 or > 10_000)
        {
            return ValidateOptionsResult.Fail(
                "Notifications:SubscriberQueueCapacity must be between 1 and 10000.");
        }

        if (options.MaximumPayloadBytes is < 1024 or > 1_048_576)
        {
            return ValidateOptionsResult.Fail(
                "Notifications:MaximumPayloadBytes must be between 1024 and 1048576.");
        }

        return ValidateOptionsResult.Success;
    }
}

namespace Gma.Framework.Realtime.Infrastructure;

using Microsoft.Extensions.Options;

internal sealed class RealtimeSubscriberQueueOptionsValidator<TMessage> :
    IValidateOptions<RealtimeSubscriberQueueOptions<TMessage>>
    where TMessage : notnull
{
    public ValidateOptionsResult Validate(string? name, RealtimeSubscriberQueueOptions<TMessage> options)
    {
        if (options.SubscriberQueueCapacity is < 1 or > RealtimeSubscriberQueueOptions<TMessage>.MaximumSubscriberQueueCapacity)
        {
            return ValidateOptionsResult.Fail(
                $"RealtimeSubscriberQueueOptions:SubscriberQueueCapacity must be between 1 and {RealtimeSubscriberQueueOptions<TMessage>.MaximumSubscriberQueueCapacity}.");
        }

        return ValidateOptionsResult.Success;
    }
}

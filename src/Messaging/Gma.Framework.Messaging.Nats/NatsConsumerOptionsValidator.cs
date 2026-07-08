namespace Gma.Framework.Messaging.Nats;

using Microsoft.Extensions.Options;

internal sealed class NatsConsumerOptionsValidator : IValidateOptions<NatsConsumerOptions>
{
    public ValidateOptionsResult Validate(string? name, NatsConsumerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DurablePrefix) &&
            !NatsConsumerDurableName.IsValidSegment(options.DurablePrefix))
        {
            return ValidateOptionsResult.Fail(
                $"{NatsConsumerOptions.SectionName}:DurablePrefix must be a lowercase kebab-case durable-name segment.");
        }

        if (options.FetchBatchSize is < 1 or > 500)
        {
            return ValidateOptionsResult.Fail($"{NatsConsumerOptions.SectionName}:FetchBatchSize must be between 1 and 500.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{NatsConsumerOptions.SectionName}:PollInterval must be positive.");
        }

        if (options.AckWait <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{NatsConsumerOptions.SectionName}:AckWait must be positive.");
        }

        if (options.MaxDeliver <= 0)
        {
            return ValidateOptionsResult.Fail($"{NatsConsumerOptions.SectionName}:MaxDeliver must be positive.");
        }

        if (options.HandlerTimeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{NatsConsumerOptions.SectionName}:HandlerTimeout must be positive.");
        }

        if (options.NakDelay <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{NatsConsumerOptions.SectionName}:NakDelay must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}

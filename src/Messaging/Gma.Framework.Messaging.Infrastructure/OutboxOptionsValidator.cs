namespace Gma.Framework.Messaging.Infrastructure;

using Microsoft.Extensions.Options;

internal sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        if (options.BatchSize <= 0)
        {
            return ValidateOptionsResult.Fail($"{OutboxOptions.SectionName}:BatchSize must be positive.");
        }

        if (options.PollIntervalMilliseconds <= 0)
        {
            return ValidateOptionsResult.Fail($"{OutboxOptions.SectionName}:PollIntervalMilliseconds must be positive.");
        }

        if (options.LockDurationMilliseconds <= 0)
        {
            return ValidateOptionsResult.Fail($"{OutboxOptions.SectionName}:LockDurationMilliseconds must be positive.");
        }

        if (options.MaxAttempts <= 0)
        {
            return ValidateOptionsResult.Fail($"{OutboxOptions.SectionName}:MaxAttempts must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}

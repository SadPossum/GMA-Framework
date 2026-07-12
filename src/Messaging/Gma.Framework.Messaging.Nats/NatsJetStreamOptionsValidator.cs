namespace Gma.Framework.Messaging.Nats;

using Microsoft.Extensions.Options;

public sealed class NatsJetStreamOptionsValidator : IValidateOptions<NatsJetStreamOptions>
{
    public ValidateOptionsResult Validate(string? name, NatsJetStreamOptions options)
    {
        List<string> failures = [];

        if (!string.IsNullOrWhiteSpace(options.StreamName) && !NatsStreamNames.IsValid(options.StreamName))
        {
            failures.Add(
                $"{NatsJetStreamOptions.SectionName}:StreamName must be 1-{NatsStreamNames.MaxLength} characters and use only ASCII letters, digits, '-' or '_'.");
        }

        if (!Enum.IsDefined(options.ManagementMode))
        {
            failures.Add($"{NatsJetStreamOptions.SectionName}:ManagementMode is invalid.");
        }

        if (!Enum.IsDefined(options.Storage))
        {
            failures.Add($"{NatsJetStreamOptions.SectionName}:Storage is invalid.");
        }

        if (options.MaxAge <= TimeSpan.Zero)
        {
            failures.Add($"{NatsJetStreamOptions.SectionName}:MaxAge must be positive.");
        }

        if (options.MaxBytes <= 0)
        {
            failures.Add($"{NatsJetStreamOptions.SectionName}:MaxBytes must be positive.");
        }

        if (options.MaxMessages <= 0)
        {
            failures.Add($"{NatsJetStreamOptions.SectionName}:MaxMessages must be positive.");
        }

        if (options.Replicas is < 1 or > 5)
        {
            failures.Add($"{NatsJetStreamOptions.SectionName}:Replicas must be between 1 and 5.");
        }

        if (options.DuplicateWindow <= TimeSpan.Zero || options.DuplicateWindow > options.MaxAge)
        {
            failures.Add(
                $"{NatsJetStreamOptions.SectionName}:DuplicateWindow must be positive and no greater than MaxAge.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

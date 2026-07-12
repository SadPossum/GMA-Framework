namespace Gma.Framework.Messaging.Infrastructure;

using Microsoft.Extensions.Options;

internal sealed class MessageJournalCleanupOptionsValidator : IValidateOptions<MessageJournalCleanupOptions>
{
    public ValidateOptionsResult Validate(string? name, MessageJournalCleanupOptions options)
    {
        List<string> failures = [];

        if (options.CleanupInterval < TimeSpan.FromSeconds(1))
        {
            failures.Add($"{MessageJournalCleanupOptions.SectionName}:CleanupInterval must be at least one second.");
        }

        if (options.BatchSize is < 1 or > 10_000)
        {
            failures.Add($"{MessageJournalCleanupOptions.SectionName}:BatchSize must be between 1 and 10000.");
        }

        if (options.MaxBatchesPerStorePerCycle is < 1 or > 1_000)
        {
            failures.Add(
                $"{MessageJournalCleanupOptions.SectionName}:MaxBatchesPerStorePerCycle must be between 1 and 1000.");
        }

        if (options.ProcessedOutboxRetention <= TimeSpan.Zero)
        {
            failures.Add(
                $"{MessageJournalCleanupOptions.SectionName}:ProcessedOutboxRetention must be positive.");
        }

        if (options.ProcessedInboxRetention <= TimeSpan.Zero)
        {
            failures.Add(
                $"{MessageJournalCleanupOptions.SectionName}:ProcessedInboxRetention must be positive.");
        }

        if (options.BrokerReplayHorizon <= TimeSpan.Zero)
        {
            failures.Add($"{MessageJournalCleanupOptions.SectionName}:BrokerReplayHorizon must be positive.");
        }

        if (options.CleanupProcessedInbox &&
            options.ProcessedInboxRetention < options.BrokerReplayHorizon)
        {
            failures.Add(
                $"{MessageJournalCleanupOptions.SectionName}:ProcessedInboxRetention must be greater than or equal to BrokerReplayHorizon.");
        }

        if (options.Enabled && !options.CleanupProcessedOutbox && !options.CleanupProcessedInbox)
        {
            failures.Add(
                $"{MessageJournalCleanupOptions.SectionName} must enable processed outbox or inbox cleanup when enabled.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

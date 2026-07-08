namespace Gma.Framework.Tasks.Infrastructure;

using Microsoft.Extensions.Options;
using Gma.Framework.Tasks;

internal sealed class TaskWorkerOptionsValidator : IValidateOptions<TaskWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, TaskWorkerOptions options)
    {
        List<string> failures = [];

        if (options.BatchSize is < 1 or > 500)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:BatchSize must be between 1 and 500.");
        }

        if (options.MaxConcurrency is < 1 or > 100)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:MaxConcurrency must be between 1 and 100.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:PollInterval must be positive.");
        }

        if (options.LeaseDuration <= TimeSpan.Zero)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:LeaseDuration must be positive.");
        }

        if (options.HandlerTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:HandlerTimeout must be positive.");
        }

        if (options.RetryBaseDelay <= TimeSpan.Zero)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:RetryBaseDelay must be positive.");
        }

        if (options.RetryMaxDelay <= TimeSpan.Zero || options.RetryMaxDelay < options.RetryBaseDelay)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:RetryMaxDelay must be positive and greater than or equal to RetryBaseDelay.");
        }

        if (options.TimeoutScannerPollInterval <= TimeSpan.Zero)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:TimeoutScannerPollInterval must be positive.");
        }

        if (options.StaleHeartbeatTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:StaleHeartbeatTimeout must be positive.");
        }

        if (options.TimeoutScannerBatchSize is < 1 or > 500)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:TimeoutScannerBatchSize must be between 1 and 500.");
        }

        if (options.MetricsSamplerPollInterval <= TimeSpan.Zero)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:MetricsSamplerPollInterval must be positive.");
        }

        if (options.WorkerGroups is null)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}:WorkerGroups must not be null.");
        }
        else
        {
            foreach (string workerGroup in options.WorkerGroups)
            {
                try
                {
                    _ = TaskNames.NormalizeWorkerGroup(workerGroup, nameof(options.WorkerGroups));
                }
                catch (ArgumentException exception)
                {
                    failures.Add($"{TaskWorkerOptions.SectionName}:WorkerGroups contains an invalid value. {exception.Message}");
                }
            }
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(options.WorkerId))
            {
                _ = TaskNames.NormalizeWorkerId(options.WorkerId, nameof(options.WorkerId));
            }

            if (!string.IsNullOrWhiteSpace(options.NodeId))
            {
                _ = TaskNames.NormalizeWorkerId(options.NodeId, nameof(options.NodeId));
            }
        }
        catch (ArgumentException exception)
        {
            failures.Add($"{TaskWorkerOptions.SectionName}: {exception.Message}");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

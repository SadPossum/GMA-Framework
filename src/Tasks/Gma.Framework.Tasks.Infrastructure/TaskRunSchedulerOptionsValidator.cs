namespace Gma.Framework.Tasks.Infrastructure;

using Microsoft.Extensions.Options;
using Gma.Framework.Tasks;

internal sealed class TaskRunSchedulerOptionsValidator : IValidateOptions<TaskRunSchedulerOptions>
{
    public ValidateOptionsResult Validate(string? name, TaskRunSchedulerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add("Tasks:Scheduler:PollInterval must be positive.");
        }

        try
        {
            _ = TaskNames.NormalizeOptionalActor(options.RequestedBy, nameof(options.RequestedBy));
        }
        catch (ArgumentException exception)
        {
            failures.Add(exception.Message);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

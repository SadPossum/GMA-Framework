namespace Gma.Framework.Cqrs.Infrastructure;

public sealed class CommandOutcomeObservationOptions
{
    public const string SectionName = "Cqrs:OutcomeObservation";
    public static readonly TimeSpan MinimumTimeout = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(1);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public static bool IsValid(CommandOutcomeObservationOptions options) =>
        options.Timeout >= MinimumTimeout &&
        options.Timeout <= MaximumTimeout;
}
